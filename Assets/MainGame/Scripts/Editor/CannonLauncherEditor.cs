using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view authoring for <see cref="CannonLauncher"/>.
///
/// A cannon is placed by dragging, not by typing: the landing handle says where the player
/// comes down, the apex handle says how high the shot goes to get there, and the arc between
/// them redraws live off the same <c>ArcPointAt</c> the flight itself samples. What the
/// designer lines up against the level's tiles IS the path the player will take — there is no
/// second copy of the curve here that could drift out of step with the runtime one.
///
/// The landing handle snaps to whole cells on release, matching <see cref="GridWorld"/>: the
/// player has to come to rest on a cell centre for the commands queued after the flight to
/// stay in step with the grid, so a handle dropped between two cells is rounded rather than
/// quietly kept.
/// </summary>
[CustomEditor(typeof(CannonLauncher))]
[CanEditMultipleObjects]
public class CannonLauncherEditor : Editor
{
    // Serialized names, kept in one place so a rename in the component shows up here as a
    // null property rather than as a handle that silently stops writing anything.
    private const string k_LandingOffset = "m_LandingOffset";
    private const string k_ArcLift = "m_ArcLift";

    private void OnSceneGUI()
    {
        var cannon = (CannonLauncher)target;

        // A SerializedObject of its own, NOT the Editor's cached `serializedObject`: Unity
        // logs an error for every touch of that property from inside OnSceneGUI, once per
        // repaint, which buries the console within seconds of selecting a cannon. A local
        // one carries the same undo and multi-object behaviour without the complaint.
        var so = new SerializedObject(cannon);

        DrawArc(cannon);
        DrawLandingHandle(cannon, so);
        DrawApexHandle(cannon, so);
        DrawReadout(cannon);
    }

    // The path itself, drawn with Handles rather than Gizmos so it stays legible at any zoom
    // (a fixed-width screen-space line) and draws on top of the level art.
    private static void DrawArc(CannonLauncher cannon)
    {
        const int segments = 48;

        Handles.color = new Color(1f, 0.8f, 0.25f, 1f);

        Vector3 previous = cannon.ArcPointAt(0f);
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = cannon.ArcPointAt((float)i / segments);
            Handles.DrawAAPolyLine(3f, previous, next);
            previous = next;
        }
    }

    // The handle that answers "where does this cannon throw the player?".
    private static void DrawLandingHandle(CannonLauncher cannon, SerializedObject so)
    {
        SerializedProperty landing = so.FindProperty(k_LandingOffset);
        if (landing == null) return;

        Vector2 current = cannon.LandingPoint;
        float size = HandleUtility.GetHandleSize(current) * 0.18f;

        EditorGUI.BeginChangeCheck();

        Handles.color = new Color(0.35f, 1f, 0.45f, 1f);
        Vector3 moved = Handles.FreeMoveHandle(
            current, size, Vector3.zero, Handles.RectangleHandleCap);

        // The cell the player will actually be put down on, so a handle dragged between two
        // cells shows where it is about to round to before the mouse is released.
        Handles.DrawWireCube(
            GridWorld.SnapToCell((Vector2)moved),
            new Vector3(GridWorld.CellSize, GridWorld.CellSize, 0f));

        if (!EditorGUI.EndChangeCheck()) return;

        // Written through the SerializedProperty rather than onto the component, so the drag
        // is one undo step and a multi-selection edit behaves like every other inspector.
        so.Update();
        landing.vector2Value =
            GridWorld.SnapToCell((Vector2)moved) - (Vector2)cannon.transform.position;
        so.ApplyModifiedProperties();
    }

    // The handle that answers "how high does it go to get there?".
    //
    // Sits on the arc's own apex — the point half way along, which is where the lift is
    // measured — so dragging it up and down is exactly the quantity being edited rather than
    // a proxy for it. Movement is projected onto Y alone: the lift has no horizontal meaning,
    // and letting the handle wander sideways would make it feel like it had lost the drag.
    private static void DrawApexHandle(CannonLauncher cannon, SerializedObject so)
    {
        SerializedProperty lift = so.FindProperty(k_ArcLift);
        if (lift == null) return;

        Vector2 apex = cannon.ArcPointAt(0.5f);
        Vector2 chordMidpoint = apex - new Vector2(0f, lift.floatValue);

        // The height being edited, drawn as the distance it actually measures.
        Handles.color = new Color(1f, 0.8f, 0.25f, 0.5f);
        Handles.DrawDottedLine(chordMidpoint, apex, 3f);

        float size = HandleUtility.GetHandleSize(apex) * 0.14f;

        EditorGUI.BeginChangeCheck();

        Handles.color = new Color(1f, 0.6f, 0.15f, 1f);
        Vector3 moved = Handles.FreeMoveHandle(apex, size, Vector3.zero, Handles.CircleHandleCap);

        if (!EditorGUI.EndChangeCheck()) return;

        so.Update();
        lift.floatValue = Mathf.Max(0f, moved.y - chordMidpoint.y);
        so.ApplyModifiedProperties();
    }

    // The numbers a designer would otherwise have to work out by eye: how far the shot
    // carries, how high it climbs, and how long the player is in the air. Reach is given in
    // cells because that is the unit the sequence is written in — a shot that carries eight
    // cells is worth comparing against what a JumpRight command would have covered.
    private static void DrawReadout(CannonLauncher cannon)
    {
        Vector2 muzzle = cannon.MuzzlePoint;
        Vector2 landing = cannon.LandingPoint;
        Vector2 delta = landing - muzzle;

        var style = new GUIStyle(EditorStyles.helpBox)
        {
            normal = { textColor = Color.white },
            fontSize = 11,
        };

        string facing = cannon.LaunchFacingSign >= 0f ? "right" : "left";

        Handles.Label(
            cannon.ArcPointAt(0.5f) + new Vector2(0f, 0.6f),
            $"reach {delta.x / GridWorld.CellSize:0.#} cells   " +
            $"drop {-delta.y / GridWorld.CellSize:0.#} cells\n" +
            $"{cannon.FlightDuration:0.##}s   faces {facing}",
            style);
    }
}
