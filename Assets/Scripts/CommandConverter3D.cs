using System;
using System.Collections.Generic;
using UnityEngine;

public static class CommandConverter3D
{
    public enum FacingDirection { North, East, South, West }
    public static List<string> ConvertPathToCommands(List<Vector3> path, string startFacing, bool invertedFacing)
    {
        List<string> commands = new List<string>();

        if (path == null || path.Count < 2)
            return commands;
        FacingDirection currentFacing = (FacingDirection)DirectionUtils.StringToDirection(startFacing);

        //Invert FacingDirection
        if (invertedFacing)
        {
            if (currentFacing == FacingDirection.North)
                currentFacing = FacingDirection.South;
            else if (currentFacing == FacingDirection.East)
                currentFacing = FacingDirection.West;
            else if (currentFacing == FacingDirection.South)
                currentFacing = FacingDirection.North;
            else if (currentFacing == FacingDirection.West)
                currentFacing = FacingDirection.East;
        }
     

        Vector3Int previousBlock = ToBlockPosition(path[0]);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3Int currentBlock = ToBlockPosition(path[i]);
            Vector3Int movement = currentBlock - previousBlock;

            // Handle vertical movement first (up/down doesn't affect facing)
            int verticalSteps = Mathf.Abs(movement.y);
            if (movement.y != 0)
            {
                string verticalCommand = movement.y > 0 ? "up" : "down";
                for (int s = 0; s < verticalSteps; s++)
                {
                    commands.Add(verticalCommand);
                }
            }

            // Handle horizontal movement
            int xSteps = Mathf.Abs(movement.x);
            int zSteps = Mathf.Abs(movement.z);
            
            if (movement.x != 0 || movement.z != 0)
            {
                FacingDirection targetFacing = GetFacingDirection(movement);
                List<string> turnCommands = GetTurnCommands(currentFacing, targetFacing);
                commands.AddRange(turnCommands);
                
                // Add forward commands for each block movement
                int forwardSteps = Mathf.Max(xSteps, zSteps);
                for (int s = 0; s < forwardSteps; s++)
                {
                    commands.Add("forward");
                }
                
                currentFacing = targetFacing;
            }

            previousBlock = currentBlock;
        }

        return commands;
    }

    private static Vector3Int ToBlockPosition(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x),
            Mathf.FloorToInt(position.y),
            Mathf.FloorToInt(position.z)
        );
    }

    private static FacingDirection GetFacingDirection(Vector3Int movement)
    {
        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.z))
        {
            return movement.x > 0 ? FacingDirection.East : FacingDirection.West;
        }
        else
        {
            return movement.z > 0 ? FacingDirection.North : FacingDirection.South;
        }
    }

    private static List<string> GetTurnCommands(FacingDirection current, FacingDirection target)
    {
        List<string> turns = new List<string>();

        if (current == target)
            return turns;

        int currentInt = (int)current;
        int targetInt = (int)target;
        int diff = (targetInt - currentInt + 4) % 4;

        if (diff == 1)
        {
            turns.Add("right");
        }
        else if (diff == 3)
        {
            turns.Add("left");
        }
        else if (diff == 2)
        {
            turns.Add("right");
            turns.Add("right");
        }

        return turns;
    }
}