using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// EXAMPLE ONLY — shows how gameplay reports a collected robot part. Put this on a pickup
    /// object; when the player grabs it (trigger, click, puzzle reward…), call <see cref="Collect"/>.
    /// The Collections UI updates automatically via CollectionSaveManager.OnChanged.
    /// </summary>
    public class Example_CollectPart : MonoBehaviour
    {
        [Tooltip("Database so collection knows the unlock order. Same asset the Collections screen uses.")]
        [SerializeField] private CollectionDatabase m_Database;

        [Tooltip("Which Robo this part belongs to (RoboData.RoboId).")]
        [SerializeField] private string m_RoboId = "robo1";

        [Tooltip("Which part this pickup grants (RobotPartData.PartId).")]
        [SerializeField] private string m_PartId = "head";

        /// <summary>Call from your pickup logic. One line is all gameplay needs.</summary>
        public void Collect()
        {
            // If CollectionManager.Configure(database) ran at startup you can omit the database arg.
            CollectionManager.CollectPart(m_Database, m_RoboId, m_PartId);
            AudioManager.Instance?.PlayPickup();
        }

        // Example: auto-collect when the player enters this trigger.
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) { Collect(); gameObject.SetActive(false); }
        }
    }
}
