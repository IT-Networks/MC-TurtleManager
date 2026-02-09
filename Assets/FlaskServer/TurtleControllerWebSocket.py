from flask import Flask, request, jsonify
from flask_cors import CORS
from flask_socketio import SocketIO, emit
import threading
import json
import os
from collections import defaultdict
from datetime import datetime

app = Flask(__name__)
CORS(app, resources={r"/*": {"origins": "*"}})
socketio = SocketIO(app, cors_allowed_origins="*", async_mode='threading')

current_command = None
block_database_file = "blocks.json"
commands = defaultdict(list)  # Warteschlange pro Turtle Label
turtle_status = {}
known_blocks = {}
connected_clients = {}  # WebSocket connections

def load_blocks():
    global known_blocks
    if os.path.exists(block_database_file):
        with open(block_database_file, "r") as f:
            try:
                data = json.load(f)
                for block in data:
                    key = f"{block['x']},{block['y']},{block['z']}"
                    known_blocks[key] = block
                print(f"[INIT] {len(known_blocks)} Bloecke geladen.")
            except Exception as e:
                print("[FEHLER] Konnte blocks.json nicht laden:", e)

def save_blocks():
    with open(block_database_file, "w") as f:
        json.dump(list(known_blocks.values()), f, indent=2)

# ============ WebSocket Events ============

@socketio.on('connect')
def handle_connect():
    print(f"[WEBSOCKET] Client verbunden: {request.sid}")
    connected_clients[request.sid] = {
        'connected_at': datetime.now().isoformat(),
        'type': 'unknown'
    }

@socketio.on('disconnect')
def handle_disconnect():
    print(f"[WEBSOCKET] Client getrennt: {request.sid}")
    if request.sid in connected_clients:
        del connected_clients[request.sid]

@socketio.on('register')
def handle_register(data):
    """Register a client (Unity or Turtle)"""
    client_type = data.get('type', 'unknown')
    client_id = data.get('id', 'unknown')
    connected_clients[request.sid] = {
        'type': client_type,
        'id': client_id,
        'connected_at': datetime.now().isoformat()
    }
    print(f"[WEBSOCKET] Client registriert: {client_type} - {client_id}")
    emit('registered', {'status': 'ok', 'sid': request.sid})

@socketio.on('turtle_status')
def handle_turtle_status(data):
    """Receive real-time turtle status updates via WebSocket"""
    if not data or 'label' not in data:
        return

    label = data['label']
    turtle_status[label] = data

    # Broadcast to all Unity clients
    socketio.emit('status_update', data, broadcast=True)

    print(f"[WS STATUS] {label} @ {data.get('position')} | Fuel: {data.get('fuelLevel')} | Inv: {data.get('inventorySlotsUsed')}/16")

@socketio.on('command')
def handle_command(data):
    """Send command to specific turtle via WebSocket"""
    label = data.get('label')
    command = data.get('command')

    if label and command:
        commands[label].append(command)
        # Notify turtle immediately
        socketio.emit(f'command_{label}', {'command': command})
        print(f"[WS COMMAND] Sende '{command}' an {label}")
        return {'status': 'ok'}
    return {'status': 'error', 'message': 'Label oder Command fehlt'}

# ============ REST API (Backwards Compatibility) ============

@app.route('/')
def index():
    return "Turtle Command Server mit WebSocket laeuft!"

@app.route('/command', methods=['POST'])
def set_command():
    global current_command
    data = request.get_json()
    if not data:
        return jsonify({'status': 'error', 'message': 'Keine Daten erhalten'}), 400

    current_command = data
    print(f"[INFO] Neuer Befehl empfangen: {data}")
    return jsonify({'status': 'ok', 'message': 'Befehl gespeichert'}), 200

@app.route("/commands", methods=["POST"])
def queue_commands():
    data = request.json
    label = data.get("label")
    cmds = data.get("commands", [])
    if label:
        commands[label].extend(cmds)
        # Notify via WebSocket
        socketio.emit(f'commands_{label}', {'commands': cmds})
        print(f"[QUEUE] Für Turtle '{label}' {len(cmds)} Kommandos hinzugefügt. Gesamt in Queue: {len(commands[label])}")
        return jsonify({'status': 'ok', 'message': 'Kommandos gequeued'}), 200
    return jsonify({'status': 'error', 'message': 'Kein Label angegeben'}), 400

@app.route("/commands", methods=["GET"])
def get_all_commands():
    label = request.args.get("label")
    if label and label in commands:
        return jsonify({"commands": commands[label]})
    return jsonify({"commands": []})

@app.route("/command", methods=["GET"])
def get_next_command():
    label = request.args.get("label")
    if label and label in commands and commands[label]:
        next_cmd = commands[label].pop(0)
        print(f"[COMMAND] Turtle '{label}' bekommt Kommando: {next_cmd}")
        return jsonify({"command": next_cmd})
    return jsonify({"command": None})

@app.route('/status', methods=['POST'])
def receive_status():
    data = request.get_json()
    if not data or 'label' not in data:
        return jsonify({'status': 'error', 'message': 'Ungueltiger Status'}), 400

    label = data['label']
    turtle_status[label] = data

    # Broadcast via WebSocket
    socketio.emit('status_update', data)

    print(f"[STATUS] {label} @ {data.get('position')} | Richtung: {data.get('direction')} | Busy: {data.get('isBusy')} | Fuel: {data.get('fuelLevel')}/{data.get('maxFuel')}")
    print(f"        Inventory Slots benutzt: {data.get('inventorySlotsUsed')}/{data.get('inventorySlotsTotal')}")
    return jsonify({'status': 'ok'}), 200

@app.route('/status/<label>', methods=['GET'])
def get_status(label):
    status = turtle_status.get(label)
    if status:
        return jsonify(status)
    else:
        return jsonify({'status': 'error', 'message': 'Nicht gefunden'}), 404

@app.route('/status/all', methods=['GET'])
def get_all_status():
    return jsonify(list(turtle_status.values()))

@app.route('/report', methods=['POST'])
def receive_scan():
    global known_blocks
    data = request.get_json(force=True)
    if isinstance(data, list):
        new_blocks = 0
        for block in data:
            key = f"{block['x']},{block['y']},{block['z']}"
            if key not in known_blocks:
                known_blocks[key] = block
                new_blocks += 1

        if new_blocks > 0:
            save_blocks()
            # Broadcast new blocks via WebSocket
            socketio.emit('blocks_update', {'new_blocks': new_blocks, 'total': len(known_blocks)})
            print(f"[SCAN] {new_blocks} neue Bloecke gespeichert. Gesamt: {len(known_blocks)}")
        else:
            print("[SCAN] Keine neuen Bloecke.")

        return jsonify({"status": "ok", "new_blocks": new_blocks})
    else:
        return jsonify({"status": "error", "message": "Ungueltige Daten"}), 400

@app.route('/report', methods=['GET'])
def get_scan():
    return jsonify(list(known_blocks.values()))

@app.route('/ws/clients', methods=['GET'])
def get_connected_clients():
    """Debug endpoint to see connected WebSocket clients"""
    return jsonify(connected_clients)

if __name__ == '__main__':
    load_blocks()
    print("[INIT] Starte Server mit WebSocket Support auf Port 4999")
    print("[INIT] WebSocket Endpoint: ws://0.0.0.0:4999/socket.io/")
    socketio.run(app, host='0.0.0.0', port=4999, debug=True, allow_unsafe_werkzeug=True)
