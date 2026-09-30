using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Valle → Costruisci il posto di guardia: in fondo alla gola del passo verso Aurelia
    /// costruisce il posto di guardia della Corona con i pezzi di Hivemind:
    /// - un ripiano scavato in fondo alla gola (che era a V, senza spazio in piano), con le rive raccordate;
    /// - due palizzate dritte da una riva all'altra (verso Aurelia e verso la valle), chiuse ai lati da grossi massi;
    /// - un varco sulla strada con due pali e la sbarra;
    /// - la torre di guardia, due tende, il fuoco con il fumo, i bracieri accesi al varco, lo stendardo;
    /// - i segni di chi ci vive: rastrelliera con le armi, frecce, bersaglio e fantoccio per allenarsi,
    ///   tavolo e sgabelli, rifornimenti (barili, casse, sacchi), legna.
    /// Per ora senza soldati: le guardie vere arrivano con gli abitanti della valle.
    /// Toglie alberi e rocce della natura dove sorge il posto. Si può rilanciare: rifà tutto da capo.
    /// </summary>
    public static class PostoDiGuardia
    {
        const string CartellaKit = "Assets/HIVEMIND";
        const string NomeRadice = "Posto di guardia";

        // La palizzata attraversa la gola qui (x), la strada la passa a questa z (dalla mappa: Passo_Aurelia)
        const float XPalizzata = 1745f;
        const float XPalizzataValle = 1693.5f; // la seconda palizzata, dal lato della valle
        const float ZStrada = 96.5f;
        const float Varco = 8f;            // larghezza del varco sulla strada
        const float PassoPalizzata = 2f;   // SM_Barricade_Var4 è largo 2 m
        // Il ripiano: in fondo alla gola il terreno è a V, senza un metro in piano. Lo strumento scava un ripiano
        // largo 44 m lungo la strada (che continua a salire come prima), con le rive raccordate, e ci costruisce sopra.
        const float RipianoDa = 1692f, RipianoA = 1768f;   // da ovest (valle) a est (Aurelia)
        const float MezzaLarghezza = 22f;                  // da ZStrada verso nord e verso sud
        const float Raccordo = 12f;                        // quanto è largo il raccordo con i fianchi della gola
        static readonly Rect Area = new Rect(RipianoDa - 8f, ZStrada - MezzaLarghezza - 12f,
                                             RipianoA - RipianoDa + 16f, 2f * MezzaLarghezza + 24f); // alberi e rocce da togliere

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly List<string> mancanti = new List<string>();

        [MenuItem("Valdorso/Valle/Costruisci il posto di guardia")]
        static void Costruisci()
        {
            GameObject valle = GameObject.Find("/Valle");
            if (valle == null || Terrain.activeTerrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }
            cache.Clear();
            mancanti.Clear();

            Transform vecchio = valle.transform.Find(NomeRadice);
            if (vecchio != null) Undo.DestroyObjectImmediate(vecchio.gameObject);
            var radice = new GameObject(NomeRadice).transform;
            Undo.RegisterCreatedObjectUndo(radice.gameObject, NomeRadice);
            radice.SetParent(valle.transform, false);

            int tolti = TogliNatura(valle.transform);
            Spiana();
            int pezzi = 0;
            float quotaStrada = Altezza(XPalizzata, ZStrada);

            // ---- Due palizzate dritte da una riva all'altra del ripiano, pannelli accostati, ognuna con il varco sulla strada:
            //      a est verso Aurelia (con la sbarra), a ovest verso la valle (aperta, da lì arrivano i rifornimenti)
            var palizzata = Gruppo(radice, "Palizzata");
            int palizzate = Palizzata(palizzata, XPalizzata) + Palizzata(palizzata, XPalizzataValle);
            pezzi += palizzate;

            // ---- Il varco verso la valle: due pali e due bracieri, senza sbarra
            var varcoValle = Gruppo(radice, "Varco verso la valle");
            if (Posa(varcoValle, "SM_WoodSupportCylinder_02a", XPalizzataValle, ZStrada - Varco * 0.5f + 0.2f, 0f, 0.5f)) pezzi++;
            if (Posa(varcoValle, "SM_WoodSupportCylinder_02a", XPalizzataValle, ZStrada + Varco * 0.5f - 0.2f, 0f, 0.5f)) pezzi++;
            pezzi += Fuoco(varcoValle, "SM_Brasero_01a", XPalizzataValle + 2.5f, ZStrada - Varco * 0.5f - 1.2f, 1.1f, 6f);
            pezzi += Fuoco(varcoValle, "SM_Brasero_01a", XPalizzataValle + 2.5f, ZStrada + Varco * 0.5f + 1.2f, 1.1f, 6f);

            // ---- Il varco: due pali e la sbarra a 1,1 m
            var varco = Gruppo(radice, "Varco");
            float z0 = ZStrada - Varco * 0.5f, z1 = ZStrada + Varco * 0.5f;
            if (Posa(varco, "SM_WoodSupportCylinder_02a", XPalizzata, z0 + 0.2f, 0f, 0.5f)) pezzi++;
            if (Posa(varco, "SM_WoodSupportCylinder_02a", XPalizzata, z1 - 0.2f, 0f, 0.5f)) pezzi++;
            for (int k = 0; k < 3; k++)
            {
                float zc = z0 + 1.3f + k * 2.6f;
                if (PosaAQuota(varco, "SM_woodenPlankA_a1", new Vector3(XPalizzata - 0.3f, quotaStrada + 1.1f, zc), new Vector3(90f, 0f, 0f))) pezzi++;
            }
            // Bracieri accesi ai due lati del varco, dal lato della valle
            pezzi += Fuoco(varco, "SM_Brasero_01a", XPalizzata - 2.2f, z0 - 1.2f, 1.1f, 6f);
            pezzi += Fuoco(varco, "SM_Brasero_01a", XPalizzata - 2.2f, z1 + 1.2f, 1.1f, 6f);
            if (Posa(varco, "SM_Flag_UV_SM_Flag_UV", XPalizzata - 3f, z1 + 3f, 0f, 0.3f)) pezzi++;
            // Cavalletti di legno davanti al varco, dal lato di Aurelia
            if (Posa(varco, "SM_WoodTrestles_02a_1", XPalizzata + 4f, ZStrada - 2f, 80f, 0f)) { pezzi++; ColliderSemplice(varco); }
            if (Posa(varco, "SM_WoodTrestles_02a_1", XPalizzata + 5.5f, ZStrada + 2.2f, 100f, 0f)) { pezzi++; ColliderSemplice(varco); }

            // ---- La torre di guardia, a nord della strada, dal lato della valle
            var campo = Gruppo(radice, "Accampamento");
            if (Posa(campo, "SM_MERGED_BP_WoodTower_01a_C_UAID_3C7C3F7B6132C24B01_1599137321", 1736.2f, 108.31f, 90f, 0.3f, perno: true)) pezzi++;

            // ---- Armi e allenamento, accanto alla torre
            if (Posa(campo, "SM_WoodWeaponsStand_UV", 1740.5f, 103.5f, 90f, 0f)) pezzi++;
            if (Posa(campo, "SM_ArcheryStorage_01a", 1742f, 105f, 90f, 0f)) pezzi++;
            if (Posa(campo, "SM_StrawArcheryTarget_01a", 1712f, 116.5f, 180f, 0f)) pezzi++;
            if (Posa(campo, "SM_Garrison_Dummy", 1715.5f, 113f, 150f, 0f)) pezzi++;

            // ---- Rifornimenti ai piedi della torre
            if (Posa(campo, "SM_barrelClosedA_a1", 1727f, 114f, 0f, 0f)) pezzi++;
            if (Posa(campo, "SM_barrelOpenedA_a1", 1728f, 115f, 30f, 0f)) pezzi++;
            if (Posa(campo, "SM_barrelClosedA_a1", 1726.6f, 115.4f, 70f, 0f)) pezzi++;
            if (Posa(campo, "SM_crateA_a1", 1722f, 113f, 10f, 0f)) pezzi++;
            if (Posa(campo, "SM_crateA_a1", 1722.9f, 113.3f, -15f, 0f)) pezzi++;
            if (Posa(campo, "SM_FoodStorage_01a", 1724f, 108f, 0f, 0f)) pezzi++;
            if (Posa(campo, "SM_burlapSackSetA_a1", 1720f, 109f, 40f, 0f)) pezzi++;

            // ---- Le tende e il fuoco, a sud della strada
            if (Posa(campo, "SM_Camp_02", 1724f, 80f, 80f, 0.1f)) pezzi++;
            if (Posa(campo, "SM_Camp_04", 1711f, 82f, 110f, 0.1f)) pezzi++;
            pezzi += Fuoco(campo, "SM_FireCamp_01b", 1716f, 89f, 0.6f, 9f, fumo: true);
            if (Posa(campo, "SM_WoodBench_01a_1", 1716f, 85.5f, 90f, 0f)) pezzi++;
            if (Posa(campo, "SM_Outskirts_CuttedWoodTrunk", 1712.5f, 89f, 0f, 0f)) pezzi++;
            if (Posa(campo, "SM_Outskirts_CuttedWoodTrunk", 1719.5f, 89.5f, 40f, 0f)) pezzi++;
            if (Posa(campo, "SM_firewoodSetA_a1", 1704f, 80f, 20f, 0f)) pezzi++;
            if (Posa(campo, "SM_WoodTable_02a", 1732f, 86f, 10f, 0f)) pezzi++;
            if (Posa(campo, "SM_WoodStool_01a", 1731f, 84.5f, 0f, 0f)) pezzi++;
            if (Posa(campo, "SM_WoodStool_01a", 1733.2f, 87.4f, 30f, 0f)) pezzi++;
            if (Posa(campo, "SM_WoodDrinkingHorn_UV", 1732f, 86.2f, 0f, -0.9f)) pezzi++; // sul tavolo
            if (Posa(campo, "SM_Camp_Drum", 1735f, 82f, 0f, 0f)) pezzi++;
            if (Posa(campo, "SM_FoodBagPack_01a", 1718f, 79f, 60f, 0f)) pezzi++;
            if (Posa(campo, "SM_WoodCart_01b", 1700f, 102f, 75f, 0f)) pezzi++;

            // ---- Più vita (30/09): la tenda del capitano, la cucina, chi ha provato a passare, luci lungo la palizzata
            var vita = Gruppo(radice, "Vita del posto");
            // La tenda grande del capitano, a nord-ovest, con il suo tavolo
            if (Posa(vita, "SM_Camp_01", 1701f, 109f, 0f, 0.1f)) pezzi++;
            if (Posa(vita, "SM_WoodTable_02a", 1707.5f, 104.5f, 90f, 0f)) pezzi++;
            if (Posa(vita, "SM_WoodChair_01a", 1707.5f, 103f, 0f, 0f)) pezzi++;
            if (Posa(vita, "SM_Candle_01c", 1707.3f, 104.6f, 0f, -0.9f)) pezzi++;
            if (Posa(vita, "SM_WoodChest_01a_1", 1704f, 103.5f, 90f, 0f)) pezzi++;
            // La cucina accanto al fuoco: pentolone, cavalletto con la carne, acqua, pentole
            if (Posa(vita, "SM_Cauldron_UV", 1719.2f, 91.8f, 0f, 0f)) pezzi++;
            if (Posa(vita, "SM_WoodTrestles_01a", 1721.5f, 88f, 30f, 0f)) { pezzi++; ColliderSemplice(vita); }
            if (Posa(vita, "SM_PieceMeat_UV", 1732.2f, 86f, 20f, -0.9f)) pezzi++;      // sul tavolo
            if (Posa(vita, "SM_WoodWaterBucket_UV", 1720.5f, 84.5f, 0f, 0f)) pezzi++;
            if (Posa(vita, "SM_MetalKitPot_02d", 1718.5f, 86.3f, 0f, 0f)) pezzi++;
            if (Posa(vita, "SM_StonePatch_01a_1", 1714f, 91.5f, 45f, 0.1f)) pezzi++;
            // Chi ha provato a lasciare la valle senza permesso: la gogna e la gabbia vicino al varco
            if (Posa(vita, "SM_PilloryStocks", 1736f, 88.5f, 90f, 0f)) pezzi++;
            if (Posa(vita, "SM_Cage_UV", 1738.5f, 83f, 15f, 0f)) pezzi++;
            // Legna e ascia, fieno e corde vicino al carretto, altri barili
            if (Posa(vita, "SM_WoodAxe_UV", 1706.5f, 77.5f, 70f, 0f)) pezzi++;
            if (Posa(vita, "SM_Outskirts_CuttedWoodTrunk", 1706f, 76.5f, 0f, 0f)) pezzi++;
            if (Posa(vita, "SM_HayPiles_01a", 1702f, 89.5f, 20f, 0f)) pezzi++;
            if (Posa(vita, "SM_RopeDebris_01a_1", 1702.5f, 100f, 0f, 0.05f)) pezzi++;
            if (Posa(vita, "SM_WoodBarrel_01a", 1725.5f, 78.5f, 0f, 0f)) pezzi++;
            if (Posa(vita, "SM_WoodBarrel_01b", 1726.3f, 77.6f, 50f, 0f)) pezzi++;
            if (Posa(vita, "SM_FoodStorage_02a", 1722.5f, 76.2f, 0f, 0f)) pezzi++;
            // Due bracieri lungo la palizzata, così di notte si vede tutta la linea
            pezzi += Fuoco(vita, "SM_Brasero_02b", 1742.3f, 84f, 0.9f, 7f);
            pezzi += Fuoco(vita, "SM_Brasero_02b", 1742.3f, 111f, 0.9f, 7f);

            // ---- Più guarnigione (30/09): la seconda torre a sud-est e altre due tende
            if (Posa(vita, "SM_WoodTower_02a", 1739.5f, 77f, 0f, 0.3f)) pezzi++;
            if (Posa(vita, "SM_Camp_03", 1731f, 78.5f, 70f, 0.1f)) pezzi++;
            if (Posa(vita, "SM_Camp_02", 1698.5f, 86f, 100f, 0.1f)) pezzi++;
            if (Posa(vita, "SM_WoodBench_01a_1", 1701.8f, 85.5f, 10f, 0f)) pezzi++;

            EditorSceneManager.MarkSceneDirty(valle.scene);
            string rapporto = $"{pezzi} pezzi (palizzata: {palizzate}), tolti {tolti} tra alberi e rocce.";
            if (mancanti.Count > 0) rapporto += "\nNon trovati: " + string.Join(", ", mancanti);
            Debug.Log("[Valdorso] Posto di guardia: " + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Posto di guardia costruito.\n\n" + rapporto + "\n\nRicorda: Ctrl+S sulla scena.", "OK");
        }

        // ------------------------------------------------------------------ pezzi

        /// <summary>
        /// Al pezzo appena posato (l'ultimo figlio del gruppo) toglie i collider del modello, fatti di gambe storte
        /// e travi sottili che spingevano il personaggio sotto il terreno, e mette una sola scatola piena
        /// dal suolo alla cima: saltandoci sopra ci si sta in piedi.
        /// </summary>
        static void ColliderSemplice(Transform gruppo)
        {
            Transform pezzo = gruppo.GetChild(gruppo.childCount - 1);
            // Si misura il pezzo dritto (senza la sua rotazione), così la scatola lo avvolge giusta
            Quaternion rotazione = pezzo.rotation;
            pezzo.rotation = Quaternion.identity;
            bool ok = Riquadro(pezzo, out Bounds b);
            Vector3 centro = ok ? pezzo.InverseTransformPoint(b.center) : Vector3.zero;
            Vector3 dim = ok ? pezzo.InverseTransformVector(b.size) : Vector3.one;
            pezzo.rotation = rotazione;
            if (!ok) return;
            foreach (Collider c in pezzo.GetComponentsInChildren<Collider>(true)) c.enabled = false; // spenti, non cancellati: il pezzo resta un prefab pulito
            var box = pezzo.gameObject.AddComponent<BoxCollider>();
            box.center = centro;
            box.size = new Vector3(Mathf.Abs(dim.x), Mathf.Abs(dim.y), Mathf.Abs(dim.z));
        }

        /// <summary>Una palizzata dritta alla x data, da una riva all'altra del ripiano, con il varco sulla strada e i massi ai lati.</summary>
        static int Palizzata(Transform gruppo, float x)
        {
            int n = 0;
            foreach (int verso in new[] { 1, -1 })
            {
                float z = ZStrada + verso * (Varco * 0.5f + 0.95f);
                for (int i = 0; Mathf.Abs(z - ZStrada) < MezzaLarghezza + 1f; i++, z += verso * 1.9f)
                {
                    string pezzo = i % 6 == 5 ? "SM_Barricade_Var3" : "SM_Barricade_Var4";
                    if (Posa(gruppo, pezzo, x, z, 90f + Random.Range(-1.5f, 1.5f), 0.35f)) n++;
                }
                // Dove la palizzata incontra il fianco della gola: grossi massi, così non resta un buco
                if (Posa(gruppo, "SM_bigRockB_a1", x + 0.5f, z + verso * 1.2f, Random.Range(0f, 360f), 0.8f, 1.6f)) n++;
                if (Posa(gruppo, "SM_bigRockA_a1", x - 2.5f, z + verso * 3.5f, Random.Range(0f, 360f), 1f, 1.4f)) n++;
                if (Posa(gruppo, "SM_Boulder_01a", x + 3f, z + verso * 2.5f, Random.Range(0f, 360f), 0.3f)) n++;
            }
            return n;
        }

        static Transform Gruppo(Transform radice, string nome)
        {
            var g = new GameObject(nome).transform;
            g.SetParent(radice, false);
            return g;
        }

        /// <summary>Posa un pezzo con la base sul terreno (affondato di 'affondo' m) e il centro in x, z.</summary>
        static bool Posa(Transform genitore, string nome, float x, float z, float gradiY, float affondo, float scala = 1f, bool perno = false)
        {
            GameObject prefab = Carica(nome);
            if (prefab == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, genitore);
            go.transform.rotation = Quaternion.Euler(0f, gradiY, 0f);
            go.transform.localScale *= scala;
            go.transform.position = new Vector3(x, 0f, z);
            if (Riquadro(go.transform, out Bounds b))
            {
                float terra = Altezza(b.center.x, b.center.z);
                // Sui pendii si prende il punto più basso sotto il pezzo, così nessun angolo resta sospeso
                terra = Mathf.Min(terra, Altezza(b.min.x, b.min.z), Altezza(b.max.x, b.max.z),
                                  Altezza(b.min.x, b.max.z), Altezza(b.max.x, b.min.z));
                go.transform.position += new Vector3(x - b.center.x, terra - affondo - b.min.y, z - b.center.z);
                // Con 'perno' x e z sono quelle del perno del modello (posizione scelta a mano nella scena), non del centro
                if (perno) go.transform.position = new Vector3(x, go.transform.position.y, z);
            }
            return true;
        }

        /// <summary>Posa un pezzo con il centro esattamente in un punto (per la sbarra).</summary>
        static bool PosaAQuota(Transform genitore, string nome, Vector3 centro, Vector3 gradi)
        {
            GameObject prefab = Carica(nome);
            if (prefab == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, genitore);
            go.transform.rotation = Quaternion.Euler(gradi);
            go.transform.position = centro;
            if (Riquadro(go.transform, out Bounds b)) go.transform.position += centro - b.center;
            return true;
        }

        /// <summary>Un braciere o un fuoco da campo acceso: fiamma, fumo (facoltativo) e una luce calda senza ombre.</summary>
        static int Fuoco(Transform genitore, string nome, float x, float z, float altezzaFiamma, float portata, bool fumo = false)
        {
            if (!Posa(genitore, nome, x, z, 0f, 0f)) return 0;
            Transform pezzo = genitore.GetChild(genitore.childCount - 1);
            Riquadro(pezzo, out Bounds b);
            Vector3 fiamma = new Vector3(x, b.min.y + altezzaFiamma, z);
            int n = 1;
            GameObject fuoco = Carica("PS_FireParticle");
            if (fuoco != null)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(fuoco, pezzo);
                f.transform.position = fiamma;
                n++;
            }
            if (fumo)
            {
                GameObject s = Carica("PS_SmokeParticle");
                if (s != null)
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(s, pezzo);
                    f.transform.position = fiamma + Vector3.up * 0.5f;
                    n++;
                }
            }
            var luce = new GameObject("Luce del fuoco").AddComponent<Light>();
            luce.transform.SetParent(pezzo, false);
            luce.transform.position = fiamma + Vector3.up * 0.3f;
            luce.type = LightType.Point;
            luce.color = new Color(1f, 0.62f, 0.3f);
            luce.intensity = 2.5f;
            luce.range = portata;
            luce.shadows = LightShadows.None;
            return n;
        }

        // ------------------------------------------------------------------ terreno

        /// <summary>
        /// Scava il ripiano in fondo alla gola: dentro il rettangolo il terreno va alla quota della strada
        /// (che sale da ovest a est come prima); tutto intorno un raccordo morbido verso i fianchi.
        /// La quota si prende dal terreno com'era, quindi rilanciare lo strumento non scava ogni volta di più.
        /// </summary>
        static void Spiana()
        {
            float quotaOvest = QuotaOriginale(RipianoDa), quotaEst = QuotaOriginale(RipianoA);
            float zMin = ZStrada - MezzaLarghezza, zMax = ZStrada + MezzaLarghezza;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData d = t.terrainData;
                Vector3 pos = t.GetPosition(), dim = d.size;
                int res = d.heightmapResolution;
                float passo = dim.x / (res - 1);
                int x0 = Mathf.Clamp(Mathf.FloorToInt((RipianoDa - Raccordo - pos.x) / passo), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((RipianoA + Raccordo - pos.x) / passo), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((zMin - Raccordo - pos.z) / passo), 0, res - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((zMax + Raccordo - pos.z) / passo), 0, res - 1);
                if (x1 <= x0 || z1 <= z0) continue;

                Undo.RegisterCompleteObjectUndo(d, "Ripiano del posto di guardia");
                float[,] h = d.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
                for (int j = 0; j <= z1 - z0; j++)
                    for (int i = 0; i <= x1 - x0; i++)
                    {
                        float wx = pos.x + (x0 + i) * passo, wz = pos.z + (z0 + j) * passo;
                        float dx = Mathf.Max(RipianoDa - wx, 0f, wx - RipianoA);
                        float dz = Mathf.Max(zMin - wz, 0f, wz - zMax);
                        float fuori = Mathf.Sqrt(dx * dx + dz * dz);
                        if (fuori >= Raccordo) continue;
                        float peso = 1f - Liscio(fuori / Raccordo);
                        float quota = Mathf.Lerp(quotaOvest, quotaEst, Mathf.InverseLerp(RipianoDa, RipianoA, wx));
                        float voluta = (quota - pos.y) / dim.y;
                        h[j, i] = Mathf.Lerp(h[j, i], voluta, peso);
                    }
                d.SetHeights(x0, z0, h);
            }
        }

        /// <summary>La quota della strada in un punto: la prima volta si misura e si ricorda nella scena (Quote del ripiano).</summary>
        static float QuotaOriginale(float x)
        {
            var memoria = GameObject.Find("/Valle/Quote del ripiano");
            if (memoria == null)
            {
                memoria = new GameObject("Quote del ripiano");
                memoria.transform.SetParent(GameObject.Find("/Valle").transform, false);
                memoria.hideFlags = HideFlags.HideInHierarchy;
            }
            Transform voce = memoria.transform.Find(x.ToString("0"));
            if (voce == null)
            {
                voce = new GameObject(x.ToString("0")).transform;
                voce.SetParent(memoria.transform, false);
                // La strada in fondo alla gola: il punto più basso lungo la sezione, vicino a ZStrada
                float minimo = float.MaxValue;
                for (float z = ZStrada - 6f; z <= ZStrada + 6f; z += 1f) minimo = Mathf.Min(minimo, Altezza(x, z));
                voce.position = new Vector3(x, minimo, ZStrada);
            }
            return voce.position.y;
        }

        static float Liscio(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ natura

        /// <summary>Toglie alberi del terreno e rocce e piante della natura dentro l'area del posto di guardia.</summary>
        static int TogliNatura(Transform valle)
        {
            int n = 0;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData d = t.terrainData;
                Vector3 pos = t.GetPosition(), dim = d.size;
                var restano = new List<TreeInstance>();
                foreach (TreeInstance albero in d.treeInstances)
                {
                    Vector3 p = pos + Vector3.Scale(albero.position, dim);
                    if (Area.Contains(new Vector2(p.x, p.z))) { n++; continue; }
                    restano.Add(albero);
                }
                if (restano.Count != d.treeInstances.Length)
                {
                    Undo.RecordObject(d, "Alberi del posto di guardia");
                    d.treeInstances = restano.ToArray();
                }
            }

            Transform natura = valle.Find("Natura");
            if (natura == null) return n;
            var daTogliere = new List<GameObject>();
            foreach (Transform t in natura.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                if (Area.Contains(new Vector2(t.position.x, t.position.z))) daTogliere.Add(t.gameObject);
            }
            foreach (GameObject g in daTogliere) { Undo.DestroyObjectImmediate(g); n++; }
            return n;
        }

        // ------------------------------------------------------------------ utilità

        static float Altezza(float x, float z)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 pos = t.GetPosition();
                Vector3 dim = t.terrainData.size;
                if (x >= pos.x && x <= pos.x + dim.x && z >= pos.z && z <= pos.z + dim.z)
                    return pos.y + t.SampleHeight(new Vector3(x, 0f, z));
            }
            return 0f;
        }

        static bool Riquadro(Transform radice, out Bounds riquadro)
        {
            bool trovato = false;
            riquadro = new Bounds();
            foreach (Renderer r in radice.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!trovato) { riquadro = r.bounds; trovato = true; }
                else riquadro.Encapsulate(r.bounds);
            }
            return trovato;
        }

        static GameObject Carica(string nome)
        {
            if (cache.TryGetValue(nome, out GameObject trovato)) return trovato;
            GameObject prefab = null;
            foreach (string guid in AssetDatabase.FindAssets(nome + " t:Prefab", new[] { CartellaKit }))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(percorso) != nome) continue;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                break;
            }
            if (prefab == null && !mancanti.Contains(nome)) mancanti.Add(nome);
            cache[nome] = prefab;
            return prefab;
        }
    }
}