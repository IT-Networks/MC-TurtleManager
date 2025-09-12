using System;
using System.Collections.Generic;
using UnityEngine;

public static class DirectionUtils
{
    public enum FacingDirection { North, East, South, West }

    private static readonly Dictionary<string, FacingDirection> DirectionMap = new Dictionary<string, FacingDirection>(StringComparer.OrdinalIgnoreCase)
    {
        {"north", FacingDirection.North},
        {"east", FacingDirection.East},
        {"south", FacingDirection.South},
        {"west", FacingDirection.West},
        {"n", FacingDirection.North},
        {"e", FacingDirection.East},
        {"s", FacingDirection.South},
        {"w", FacingDirection.West},
        {"forward", FacingDirection.South},
        {"right", FacingDirection.West},
        {"back", FacingDirection.South},
        {"left", FacingDirection.East},
        {"up", FacingDirection.North},    // Common alternatives
        {"down", FacingDirection.South},
        {"0", FacingDirection.North},     // Numeric alternatives
        {"1", FacingDirection.East},
        {"2", FacingDirection.South},
        {"3", FacingDirection.West}
    };

    // Convert string to FacingDirection
    public static FacingDirection StringToDirection(string directionStr)
    {
        if (string.IsNullOrWhiteSpace(directionStr))
        {
            Debug.LogWarning("Empty direction string, defaulting to North");
            return FacingDirection.North;
        }

        if (DirectionMap.TryGetValue(directionStr.Trim(), out FacingDirection direction))
        {
            return direction;
        }

        Debug.LogWarning($"Unknown direction string: '{directionStr}', defaulting to North");
        return FacingDirection.North;
    }

    // Convert FacingDirection to normalized Vector3
    public static Vector3 DirectionToVector(FacingDirection direction)
    {
        switch (direction)
        {
            case FacingDirection.North: return Vector3.forward;
            case FacingDirection.East: return Vector3.right;
            case FacingDirection.South: return Vector3.back;
            case FacingDirection.West: return Vector3.left;
            default: return Vector3.forward;
        }
    }

    // Set GameObject's rotation to face a specific direction
    public static void SetFacingDirection(GameObject obj, FacingDirection direction)
    {
        Vector3 lookDirection = DirectionToVector(direction);
        if (lookDirection != Vector3.zero)
        {
            obj.transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    // Set GameObject's rotation using string direction
    public static void SetFacingDirection(GameObject obj, string directionStr)
    {
        SetFacingDirection(obj, StringToDirection(directionStr));
    }

    // Get current facing direction from a transform
    public static FacingDirection GetFacingDirection(Transform transform)
    {
        Vector3 forward = transform.forward;
        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        angle = (angle + 360) % 360; // Normalize to 0-360

        if (angle >= 45 && angle < 135) return FacingDirection.East;
        if (angle >= 135 && angle < 225) return FacingDirection.South;
        if (angle >= 225 && angle < 315) return FacingDirection.West;
        return FacingDirection.North;
    }

    // Convert direction to human-readable string
    public static string DirectionToString(FacingDirection direction)
    {
        return direction.ToString().ToLower();
    }
}