using UnityEngine;

/// <summary>
/// Single source of truth for the level grid.
///
/// Every level's scene <c>Grid</c> sits at world (0.5, 0.5) with a 1-unit cell, which
/// puts CELL CENTRES ON INTEGER WORLD COORDINATES and cell boundaries on half-integers.
/// A 1x1 object resting in a cell therefore has an integer position on both axes, and a
/// body standing on a floor has its feet on the half-integer boundary below it.
///
/// Anything that occupies a cell — the player, push bricks, moving blocks — must come to
/// rest on an integer pair. Fractional positions are the symptom of physics depenetration
/// or of collider geometry being used to answer a grid question; both produce cells that
/// are half-occupied, which every downstream support/blocking query then reads wrongly.
///
/// Snap through this class rather than open-coding <c>Mathf.Round</c>, so the convention
/// lives in exactly one place.
/// </summary>
public static class GridWorld
{
    /// <summary>Width and height of one cell, in world units.</summary>
    public const float CellSize = 1f;

    /// <summary>Half a cell — the distance from a cell centre to any of its edges.</summary>
    public const float HalfCell = CellSize * 0.5f;

    /// <summary>
    /// Nearest cell centre to <paramref name="position"/>. Use for any object that
    /// occupies a whole cell, on BOTH axes — snapping only X leaves the object free to
    /// settle half a cell up or down, straddling two rows.
    /// </summary>
    public static Vector2 SnapToCell(Vector2 position) =>
        new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));

    /// <summary>
    /// Nearest cell centre, preserving <paramref name="position"/>'s z (so a
    /// <see cref="Transform.position"/> keeps its sorting depth).
    /// </summary>
    public static Vector3 SnapToCell(Vector3 position) =>
        new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), position.z);

    /// <summary>Centre of the cell containing <paramref name="value"/>, on one axis.</summary>
    public static float SnapAxis(float value) => Mathf.Round(value);

    /// <summary>
    /// World Y of the top edge of the cell containing <paramref name="y"/> — i.e. the
    /// surface height an object standing on that cell rests its feet on.
    /// </summary>
    public static float CellTop(float y) => Mathf.Round(y) + HalfCell;

    /// <summary>
    /// World Y of the bottom edge of the cell containing <paramref name="y"/>.
    /// </summary>
    public static float CellBottom(float y) => Mathf.Round(y) - HalfCell;

    /// <summary>
    /// True when <paramref name="position"/> already sits on a cell centre, within
    /// <paramref name="tolerance"/>. Useful in assertions and editor validation.
    /// </summary>
    public static bool IsOnCell(Vector2 position, float tolerance = 0.01f) =>
        Mathf.Abs(position.x - Mathf.Round(position.x)) <= tolerance &&
        Mathf.Abs(position.y - Mathf.Round(position.y)) <= tolerance;
}
