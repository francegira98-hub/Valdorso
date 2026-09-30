using System.Collections;
using System.Collections.Generic;
using Mirror;
using StarterAssets;
using UnityEngine;
using Valdorso.Creatures;
using Valdorso.Interazione;

namespace Valdorso.World
{
    /// <summary>
    /// Il sigillo della Corona: il velo di luce che chiude la gola del passo verso Aurelia.
    /// - Il velo si costruisce da solo seguendo il terreno: da un fianco all'altro della gola, finché il monte
    ///   non sale sopra la sua cima. È solido (non si attraversa) e ha davanti una fascia che respinge.
    /// - Chi lo tocca: il server lo conferma, toglie un po' di salute e dice al suo PC di respingerlo indietro,
    ///   con un breve stordimento e la scritta "Il sigillo della Corona ti respinge"; tutti vedono il lampo.
    /// - Pulsa con il battito del Cuore, è più forte di notte, e ronza piano (il suono è creato dal codice).
    /// Lo mette nella scena il menu Valdorso → Valle → Alza il sigillo della Corona.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class SigilloCorona : NetworkBehaviour
    {
        [Header("Forma")]
        [Tooltip("Altezza del velo sopra la strada (m)")]
        [SerializeField] float altezza = 30f;
        [Tooltip("Quanto cercare verso i fianchi della gola, da una parte e dall'altra (m)")]
        [SerializeField] float mezzaLarghezzaMassima = 90f;
        [Tooltip("Passo con cui il velo segue il terreno (m)")]
        [SerializeField] float passo = 1f;

        [Header("Chi lo tocca")]
        [SerializeField] float danno = 5f;
        [SerializeField] float spinta = 4.5f;
        [SerializeField] float stordimento = 1.2f;
        [SerializeField] float attesaTraDueTocchi = 2f;

        [Header("Suono")]
        [SerializeField] float volume = 0.35f;

        static readonly int NotteId = Shader.PropertyToID("_Notte");
        static readonly int ToccoId = Shader.PropertyToID("_Tocco");

        Material materiale;
        AudioSource ronzio;
        float zMin, zMax;
        float tocco;
        bool respinto;
        readonly Dictionary<uint, double> ultimoTocco = new Dictionary<uint, double>();

        void Awake()
        {
            materiale = GetComponent<MeshRenderer>().material;
        }

        void Start()
        {
            Costruisci(); // in Start: i terreni sono già pronti
            if (!Application.isBatchMode) PreparaRonzio();
        }

        // ------------------------------------------------------------------ forma

        /// <summary>Costruisce il velo seguendo il terreno. Lo usa anche il menu per vederlo nell'editor.</summary>
        public void Costruisci()
        {
            Vector3 o = transform.position;
            float base0 = Terreno(o.x, o.z);
            float cima = base0 + altezza;

            // Da una parte e dall'altra, finché il monte non arriva quasi alla cima del velo
            float verso(int s)
            {
                float z = o.z;
                for (float d = 0f; d <= mezzaLarghezzaMassima; d += passo)
                {
                    z = o.z + s * d;
                    if (Terreno(o.x, z) > cima - 3f) break;
                }
                return z;
            }
            zMin = verso(-1);
            zMax = verso(1);

            var vertici = new List<Vector3>();
            var uv = new List<Vector3>();
            var triangoli = new List<int>();
            int colonne = Mathf.Max(2, Mathf.CeilToInt((zMax - zMin) / passo) + 1);
            for (int c = 0; c < colonne; c++)
            {
                float z = Mathf.Lerp(zMin, zMax, c / (float)(colonne - 1));
                float suolo = Terreno(o.x, z) - 1f; // un metro sotto terra, così non resta una fessura
                float lungo = z - zMin;
                vertici.Add(transform.InverseTransformPoint(new Vector3(o.x, suolo, z)));
                vertici.Add(transform.InverseTransformPoint(new Vector3(o.x, cima, z)));
                uv.Add(new Vector3(lungo, suolo - base0, 0f));
                uv.Add(new Vector3(lungo, cima - base0, 1f));
                if (c == 0) continue;
                int a = (c - 1) * 2, b = c * 2;
                triangoli.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }

            var mesh = new Mesh { name = "Velo del sigillo", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertici);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangoli, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;

            // La fascia che respinge: 3 m davanti e dietro il velo, per tutta la larghezza
            Transform fascia = transform.Find("Fascia");
            if (fascia == null)
            {
                fascia = new GameObject("Fascia").transform;
                fascia.SetParent(transform, false);
            }
            fascia.gameObject.layer = 2; // Ignore Raycast
            var box = fascia.GetComponent<BoxCollider>();
            if (box == null) box = fascia.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            fascia.position = new Vector3(o.x, base0 + altezza * 0.5f, (zMin + zMax) * 0.5f);
            fascia.rotation = Quaternion.identity;
            box.center = Vector3.zero;
            box.size = new Vector3(3f, altezza + 2f, zMax - zMin);
            if (fascia.GetComponent<FasciaSigillo>() == null) fascia.gameObject.AddComponent<FasciaSigillo>();
        }

        static float Terreno(float x, float z)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 pos = t.GetPosition(), dim = t.terrainData.size;
                if (x >= pos.x && x <= pos.x + dim.x && z >= pos.z && z <= pos.z + dim.z)
                    return pos.y + t.SampleHeight(new Vector3(x, 0f, z));
            }
            return 0f;
        }

        // ------------------------------------------------------------------ aspetto e suono

        void Update()
        {
            if (materiale == null) return;
            materiale.SetFloat(NotteId, Notte());
            tocco = Mathf.MoveTowards(tocco, 0f, Time.deltaTime * 2.5f);
            materiale.SetFloat(ToccoId, tocco);

            // Il ronzio viene dal punto del velo più vicino a chi ascolta
            if (ronzio != null && Camera.main != null)
            {
                Vector3 c = Camera.main.transform.position;
                ronzio.transform.position = new Vector3(transform.position.x, Mathf.Clamp(c.y, transform.position.y, transform.position.y + altezza),
                                                        Mathf.Clamp(c.z, zMin, zMax));
            }
        }

        /// <summary>Quanto è notte: dal sole della scena (più è basso, più il sigillo si vede).</summary>
        static float Notte()
        {
            Light sole = RenderSettings.sun;
            if (sole == null) return 0.3f;
            float alto = Vector3.Dot(-sole.transform.forward, Vector3.up); // 1 sole a picco, 0 all'orizzonte
            return 1f - Mathf.Clamp01(alto * 3f);
        }

        void PreparaRonzio()
        {
            var go = new GameObject("Ronzio del sigillo");
            go.transform.SetParent(transform, false);
            ronzio = go.AddComponent<AudioSource>();
            ronzio.clip = CreaRonzio();
            ronzio.loop = true;
            ronzio.spatialBlend = 1f;
            ronzio.minDistance = 4f;
            ronzio.maxDistance = 45f;
            ronzio.rolloffMode = AudioRolloffMode.Logarithmic;
            ronzio.volume = volume;
            ronzio.Play();
        }

        /// <summary>Un ronzio basso che pulsa con il battito: tre note gravi e un filo di luce acuto, 3,2 secondi in ciclo.</summary>
        static AudioClip CreaRonzio()
        {
            const int freq = 44100;
            const float durata = 3.2f; // due battiti da 1,6 s: il ciclo si chiude senza scatti
            int n = Mathf.RoundToInt(freq * durata);
            var dati = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)freq;
                float f = (t % 1.6f) / 1.6f;
                float battito = Mathf.Exp(-Mathf.Pow((f - 0.05f) * 16f, 2f)) + 0.65f * Mathf.Exp(-Mathf.Pow((f - 0.24f) * 16f, 2f));
                float basso = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.3f
                            + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.15f;
                float luce = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.04f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.625f * t));
                dati[i] = (basso * (0.55f + 0.45f * battito) + luce) * 0.5f;
            }
            AudioClip clip = AudioClip.Create("Ronzio del sigillo", n, 1, freq, false);
            clip.SetData(dati, 0);
            return clip;
        }

        // ------------------------------------------------------------------ chi lo tocca

        /// <summary>Lo chiama la fascia quando il personaggio di chi gioca, su questo PC, entra nel sigillo.</summary>
        public void ToccatoDaQui(Creature chi)
        {
            if (respinto) return;
            respinto = true;
            CmdToccato();
        }

        [Command(requiresAuthority = false)]
        void CmdToccato(NetworkConnectionToClient mittente = null)
        {
            if (mittente?.identity == null) return;
            Creature chi = mittente.identity.GetComponent<Creature>();
            if (chi == null || chi.IsDead) { TargetLibero(mittente); return; }

            // Il server controlla che sia davvero vicino al velo e non tocchi troppo spesso
            Vector3 p = chi.transform.position;
            bool vicino = Mathf.Abs(p.x - transform.position.x) < 6f && p.z > zMin - 3f && p.z < zMax + 3f;
            double ora = NetworkTime.time;
            if (!vicino || (ultimoTocco.TryGetValue(mittente.identity.netId, out double prima) && ora - prima < attesaTraDueTocchi))
            {
                TargetLibero(mittente);
                return;
            }
            ultimoTocco[mittente.identity.netId] = ora;

            chi.ReceiveDamage(DamageInfo.Create(danno, DamageType.Pure, null, "il sigillo della Corona"));
            float lato = Mathf.Sign(p.x - transform.position.x); // da che parte stava: lo si rimanda da lì
            if (lato == 0f) lato = -1f;
            TargetRespingi(mittente, lato);
            RpcLampo(new Vector3(transform.position.x, p.y + 1.2f, p.z));
        }

        [TargetRpc]
        void TargetLibero(NetworkConnectionToClient target) => respinto = false;

        [TargetRpc]
        void TargetRespingi(NetworkConnectionToClient target, float lato)
        {
            GameObject io = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
            if (io == null) { respinto = false; return; }
            io.GetComponent<Interattore>()?.Avviso("Il sigillo della Corona ti respinge", "Nessuno lascia la valle senza il permesso del re");
            StartCoroutine(Respingi(io, lato));
        }

        IEnumerator Respingi(GameObject io, float lato)
        {
            var cc = io.GetComponent<CharacterController>();
            var mover = io.GetComponent<ThirdPersonController>();
            var input = io.GetComponent<StarterAssetsInputs>();
            bool eraAcceso = mover != null && mover.enabled;
            if (mover != null) mover.enabled = false;
            if (input != null) { input.move = Vector2.zero; input.jump = false; input.sprint = false; }
            // Senza il controller l'animazione resterebbe a correre sul posto: la fermiamo
            Animator anim = io.GetComponent<Animator>();
            if (anim != null)
            {
                foreach (AnimatorControllerParameter par in anim.parameters)
                    if (par.name == "Speed" && par.type == AnimatorControllerParameterType.Float) anim.SetFloat(par.nameHash, 0f);
            }

            // Indietro con un piccolo balzo, lontano dal velo
            float t = 0f, durata = 0.35f;
            Vector3 dir = new Vector3(lato, 0f, 0f);
            float yPrima = 0f;
            while (t < durata && cc != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / durata);
                float y = Mathf.Sin(k * Mathf.PI) * 0.6f;
                cc.Move(dir * (spinta / durata) * Time.deltaTime + Vector3.up * (y - yPrima));
                yPrima = y;
                yield return null;
            }
            yield return new WaitForSeconds(stordimento);
            if (mover != null && eraAcceso) mover.enabled = true;
            respinto = false;
        }

        [ClientRpc]
        void RpcLampo(Vector3 punto)
        {
            tocco = 1f;
            var luce = new GameObject("Lampo del sigillo").AddComponent<Light>();
            luce.transform.position = punto;
            luce.type = LightType.Point;
            luce.color = new Color(1f, 0.8f, 0.4f);
            luce.intensity = 6f;
            luce.range = 10f;
            luce.shadows = LightShadows.None;
            Destroy(luce.gameObject, 0.25f);
        }
    }
}