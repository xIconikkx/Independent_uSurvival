// grid structure: get/set values of type T at any point
using System.Collections.Generic;
using UnityEngine;

public class Grid2D<T>
{
    Dictionary<Vector2Int, HashSet<T>> grid = new Dictionary<Vector2Int, HashSet<T>>();

    // cache a 9 neighbor grid of vector2 offsets so we can use them more easily
    Vector2Int[] neighbourOffsets =
    {
        Vector2Int.up,
        Vector2Int.up + Vector2Int.left,
        Vector2Int.up + Vector2Int.right,
        Vector2Int.left,
        Vector2Int.zero,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.down + Vector2Int.left,
        Vector2Int.down + Vector2Int.right
    };

    // helper function so we can remove an entry without worrying
    public void Remove(Vector2Int position, T value)
    {
        // is this set in the grid? then remove it
        if (grid.TryGetValue(position, out HashSet<T> hashSet))
        {
            // remove value from this position's hashset
            hashSet.Remove(value);

            // if empty then remove this hashset entirely. no need to keep
            // HashSet<pos> in memory forever if no one is there anymore.
            if (hashSet.Count == 0)
                grid.Remove(position);
        }
    }

    // helper function so we can add an entry without worrying
    public void Add(Vector2Int position, T value)
    {
        // initialize set in grid if it's not in there yet
        if (!grid.TryGetValue(position, out HashSet<T> hashSet))
        {
            hashSet = new HashSet<T>();
            grid[position] = hashSet;
        }

        // add to it
        hashSet.Add(value);
    }

    // helper function to get set at position without worrying
    // -> cache empty HashSet to avoid allocations. it's only used in
    //    GetWithNeighbours anyway.
    static HashSet<T> emptyHashSet = new HashSet<T>();
    public HashSet<T> Get(Vector2Int position)
    {
        // return the set at position
        if (grid.TryGetValue(position, out HashSet<T> hashSet))
            return hashSet;

        // or empty new set otherwise (rebuild observers doesn't want null)
        emptyHashSet.Clear();
        return emptyHashSet;
    }

    // helper function to get at position and it's 8 neighbors without worrying
    // -> result HashSet is passed as parameter so it can be reused without
    //    allocating each time. makes it a lot faster.
    public void GetWithNeighbours(Vector2Int position, HashSet<T> result)
    {
        result.Clear();
        foreach (Vector2Int offset in neighbourOffsets)
            result.UnionWith(Get(position + offset));
    }
}