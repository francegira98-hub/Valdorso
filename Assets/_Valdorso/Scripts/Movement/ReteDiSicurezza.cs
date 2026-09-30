using System.Reflection;
using Mirror;
using StarterAssets;
using UnityEngine;

namespace Valdorso.Movement
{
    /// <summary>
    /// La rete di sicurezza: se il personaggio di chi gioca finisce sotto il terreno e sotto di lui non c'è più niente
    /// (è caduto attraverso il mondo), lo riporta subito all'ultimo punto in cui stava coi piedi per terra.
    /// Scrive in Console dove è successo, così si può trovare e sistemare il buco.
    /// Non scatta nelle grotte o sott'acqua: lì sotto di sé c'è sempre un pavimento.
    /// Va sul prefab del giocatore; lavora solo sul PC di chi gioca.
    /// </summary>
    public class ReteDiSicurezza : MonoBehaviour
    {
        [Tooltip("Quanti metri sotto il terreno prima di intervenire")]
        [SerializeField] float margine = 4f;

        static readonly FieldInfo velocitaVerticale =
            typeof(ThirdPersonController).GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

        NetworkIdentity identita;
        CharacterController cc;
        ThirdPersonController mover;
        Vector3 ultimoSicuro;
        bool haUnPunto;
        float prossimoRicordo;

        void Awake()
        {
            identita = GetComponent<NetworkIdentity>();
            cc = GetComponent<CharacterController>();
            mover = GetComponent<ThirdPersonController>();
        }

        void Update()
        {
            if (identita == null || !identita.isLocalPlayer) return;

            // Ogni mezzo secondo si ricorda dove sta, se è a terra
            if (Time.time >= prossimoRicordo)
            {
                prossimoRicordo = Time.time + 0.5f;
                if (mover != null && mover.enabled && mover.Grounded)
                {
                    ultimoSicuro = transform.position;
                    haUnPunto = true;
                }
            }

            Vector3 p = transform.position;
            bool sottoIlMondo = p.y < -500f;
            if (!sottoIlMondo && SottoIlTerreno(p, out float suolo))
            {
                // Sotto il terreno: è un buco solo se sotto i piedi non c'è nessun pavimento (grotte, cantine)
                sottoIlMondo = p.y < suolo - margine &&
                               !Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            }
            if (sottoIlMondo) Riporta(p);
        }

        void Riporta(Vector3 dove)
        {
            Vector3 destinazione = haUnPunto ? ultimoSicuro : dove;
            if (!haUnPunto && SottoIlTerreno(dove, out float suolo)) destinazione = new Vector3(dove.x, suolo + 1f, dove.z);
            Debug.LogWarning($"[Valdorso] Caduto attraverso il mondo a x {dove.x:0.0}, y {dove.y:0.0}, z {dove.z:0.0}: " +
                             $"riportato a x {destinazione.x:0.0}, y {destinazione.y:0.0}, z {destinazione.z:0.0}.");
            if (cc != null) cc.enabled = false;
            transform.position = destinazione + Vector3.up * 0.3f;
            if (cc != null) cc.enabled = true;
            if (mover != null) velocitaVerticale?.SetValue(mover, 0f);
        }

        static bool SottoIlTerreno(Vector3 p, out float suolo)
        {
            suolo = 0f;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 pos = t.GetPosition(), dim = t.terrainData.size;
                if (p.x < pos.x || p.x > pos.x + dim.x || p.z < pos.z || p.z > pos.z + dim.z) continue;
                suolo = pos.y + t.SampleHeight(p);
                return true;
            }
            return false;
        }
    }
}
