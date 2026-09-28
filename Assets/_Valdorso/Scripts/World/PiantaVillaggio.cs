using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// La pianta del villaggio di Val d'Orso (proposta di Claude, approvata da Fra il 28/09): lotti, edifici e strade.
    /// Coordinate in metri rispetto al centro del villaggio (ValleMappa.Villaggio): x verso est, y verso nord.
    /// Serve allo strumento Valdorso → Villaggio → Costruisci il villaggio e, più avanti, alla crescita a spazi e stadi.
    /// Per spostare un edificio basta cambiare qui i suoi numeri e ricostruire il villaggio.
    /// </summary>
    public static class PiantaVillaggio
    {
        public enum Tipo { Piazza, Tempio, Camposanto, Balivo, Locanda, Gilda, Addestramento, Fabbro, Orti, Casa, Fienile, Torre, Libero }

        /// <summary>Uno spazio del piano urbanistico: un rettangolo di terreno con il suo uso.</summary>
        public class Lotto
        {
            public readonly string id, nome;
            public readonly Tipo tipo;
            public readonly Rect area;

            public Lotto(string id, string nome, Tipo tipo, float x1, float x2, float y1, float y2)
            {
                this.id = id; this.nome = nome; this.tipo = tipo;
                area = Rect.MinMaxRect(x1, y1, x2, y2);
            }
        }

        /// <summary>
        /// Un edificio montato con il kit modulare di Hivemind. Angolo = il perno dell'edificio (l'angolo da cui parte
        /// il muro della facciata). Facciata: 0 = porta a nord, 90 = a est, 180 = a sud, 270 = a ovest.
        /// Moduli = lunghezza della facciata in moduli da 6 m; la profondità è sempre 6 m. Piani: 1 o 2.
        /// </summary>
        public class Edificio
        {
            public readonly string lotto, nome;
            public readonly Vector2 angolo;
            public readonly float facciata;
            public readonly int moduli, piani;

            public Edificio(string lotto, string nome, float x, float y, float facciata, int moduli, int piani)
            {
                this.lotto = lotto; this.nome = nome; angolo = new Vector2(x, y);
                this.facciata = facciata; this.moduli = moduli; this.piani = piani;
            }

            public float Lunghezza => moduli * 6f;

            /// <summary>Da coordinate dell'edificio (x lungo la facciata, z verso fuori dalla porta) a coordinate del villaggio.</summary>
            public Vector2 AlVillaggio(float x, float z)
            {
                float r = facciata * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
                return angolo + new Vector2(x * c + z * s, -x * s + z * c);
            }

            /// <summary>Il rettangolo occupato dall'edificio, in coordinate del villaggio.</summary>
            public Rect Impronta
            {
                get
                {
                    Vector2 a = AlVillaggio(0f, 0f), b = AlVillaggio(Lunghezza, 0f), c = AlVillaggio(0f, -6f), d = AlVillaggio(Lunghezza, -6f);
                    return Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x, d.x), Mathf.Min(a.y, b.y, c.y, d.y),
                                           Mathf.Max(a.x, b.x, c.x, d.x), Mathf.Max(a.y, b.y, c.y, d.y));
                }
            }
        }

        /// <summary>
        /// Un pezzo singolo (torre, pozzo, panca, recinto...) posato con il centro della sua base nel punto dato,
        /// appoggiato al terreno. rotY = rotazione intorno all'asse verticale, in gradi.
        /// Se pernoY è indicato, il perno del pezzo va a quell'altezza (invece di appoggiare il pezzo al terreno);
        /// con xzEsatti il pezzo non viene nemmeno centrato e il perno va esattamente in (x, pernoY, y).
        /// Serve per i pezzi sistemati a mano da Fra (torre, albero).
        /// </summary>
        public class Oggetto
        {
            public readonly string prefab;
            public readonly Vector2 posizione;
            public readonly float rotY;
            public readonly float pernoY;
            public readonly bool xzEsatti;

            public Oggetto(string prefab, float x, float y, float rotY = 0f, float pernoY = float.NaN, bool xzEsatti = false)
            {
                this.prefab = prefab; posizione = new Vector2(x, y); this.rotY = rotY; this.pernoY = pernoY; this.xzEsatti = xzEsatti;
            }

            public bool PernoEsatto => !float.IsNaN(pernoY);
        }

        public static readonly Lotto[] Lotti =
        {
            new Lotto("piazza",       "Piazza",                     Tipo.Piazza,        -16,  16, -12,  14),
            new Lotto("tempio",       "Tempio del Cuore",           Tipo.Tempio,        -11,  11,  18,  44),
            new Lotto("camposanto",   "Camposanto e memoriale",     Tipo.Camposanto,    -34, -15,  24,  44),
            new Lotto("balivo",       "Casa del Balivo",            Tipo.Balivo,         20,  46,  -8,  16),
            new Lotto("locanda",      "Locanda",                    Tipo.Locanda,        12,  44, -48, -22),
            new Lotto("gilda",        "Gilda degli avventurieri",   Tipo.Gilda,         -46, -22,  -8,  10),
            new Lotto("addestramento","Campo d'addestramento",      Tipo.Addestramento, -46, -26,  12,  21),
            new Lotto("fabbro",       "Fabbro",                     Tipo.Fabbro,         26,  44,  23,  40),
            new Lotto("orti",         "Orti comuni",                Tipo.Orti,          -42, -22, -24, -15),
            new Lotto("casa1",        "Casa dei coloni 1",          Tipo.Casa,          -42, -26, -44, -28),
            new Lotto("casa2",        "Casa dei coloni 2",          Tipo.Casa,          -20,  -8, -42, -30),
            new Lotto("casa3",        "Casa dei coloni 3",          Tipo.Casa,           -4,   8, -44, -32),
            new Lotto("casa4",        "Casa dei coloni 4",          Tipo.Casa,          -62, -50,   0,  14),
            new Lotto("casa5",        "Casa dei coloni 5",          Tipo.Casa,           48,  62,  23,  38),
            new Lotto("fienile",      "Fienile e recinto",          Tipo.Fienile,        46,  70, -66, -46),
            new Lotto("torre",        "Torre di vedetta",           Tipo.Torre,          62,  70, -32, -24),
            new Lotto("libero1",      "Lotto libero 1",             Tipo.Libero,         50,  64,  -8,   8),
            new Lotto("libero2",      "Lotto libero 2",             Tipo.Libero,        -62, -48, -32, -18),
            new Lotto("libero3",      "Lotto libero 3",             Tipo.Libero,         30,  46,  44,  58),
            new Lotto("libero4",      "Lotto libero 4",             Tipo.Libero,        -20,  -6, -62, -50),
            new Lotto("mulino",       "Mulino",                     Tipo.Casa,         -152, -140,  16,  30),
        };

        public static readonly Edificio[] Edifici =
        {
            // Tempio provvisorio: sala lunga di legno su due piani, porta sulla piazza (a sud). Sarà rifatto in pietra.
            new Edificio("tempio",  "Tempio del Cuore",          9,  24, 180, 3, 2),
            new Edificio("balivo",  "Casa del Balivo",          24,  -4, 270, 2, 2),
            new Edificio("balivo",  "Casa del Balivo, ala",     24,   8, 270, 1, 1),
            new Edificio("locanda", "Locanda",                  16, -24,   0, 3, 2),
            new Edificio("gilda",   "Gilda degli avventurieri",-26,   7,  90, 2, 2),
            new Edificio("fabbro",  "Bottega del fabbro",       28,  26, 270, 1, 1),
            new Edificio("casa1",   "Casa dei coloni 1",       -40, -31,   0, 2, 1),
            new Edificio("casa2",   "Casa dei coloni 2",       -10, -33,  90, 1, 1),
            new Edificio("casa3",   "Casa dei coloni 3",        -2, -41, 270, 1, 1),
            new Edificio("casa4",   "Casa dei coloni 4",       -52,  11,  90, 1, 1),
            new Edificio("casa5",   "Casa dei coloni 5",        61,  25, 180, 2, 2),
            // Fienile provvisorio: una casa bassa; il fienile vero con il kit del fienile arriva con gli oggetti.
            new Edificio("fienile", "Fienile",                  50, -48,   0, 2, 1),
            // Mulino sulla riva est del fiume, porta verso il sentiero; la ruota ad acqua arriverà più avanti.
            new Edificio("mulino",  "Mulino",                 -142,  26,  90, 1, 1),
        };

        /// <summary>Gli oggetti degli esterni: piazza, cortili, campo, orti, camposanto, recinti.</summary>
        public static readonly List<Oggetto> Oggetti = CreaOggetti();

        static List<Oggetto> CreaOggetti()
        {
            var o = new List<Oggetto>();
            void Metti(string prefab, float x, float y, float rot = 0f) => o.Add(new Oggetto(prefab, x, y, rot));

            // Torre di vedetta all'ingresso dalla strada del passo, con un braciere e due barili
            // Posizione sistemata a mano da Fra il 28/09: abbassata per nascondere il basamento di roccia del pezzo
            o.Add(new Oggetto("SM_MERGED_BP_WoodTower_01a_C_UAID_3C7C3F7B6132C24B01_1599137321", 67.41f, -27.86f, 0f, pernoY: 67.35f, xzEsatti: true));
            Metti("SM_Brasero_01a", 61.5f, -24.5f);
            Metti("SM_barrelClosedA_a1", 70.5f, -24f);
            Metti("SM_barrelOpenedA_a1", 71.3f, -25.2f, 40);

            // Piazza: pozzo, albero del consiglio con le panche, gogna, mercato (albo degli editti e bacheca della Gilda: al 6.5.6)
            Metti("SM_OldWell_UV_SM_OldWell_Stone", 0, -1);
            o.Add(new Oggetto("SM_EuropeanBeech_L_02", -11, 9, 0f, pernoY: 66f));   // abbassato da Fra il 28/09
            Metti("SM_benchA_a1", -7.5f, 6.5f, 0);
            Metti("SM_benchA_a1", -14f, 4f, 90);
            Metti("SM_PilloryStocks", 12, -8, 20);
            Metti("SM_FoodStorage_02a", -10, -10);
            Metti("SM_FoodStorage_02a", -4.5f, -10);
            Metti("SM_FoodStorage_01a", 4, -10);
            Metti("SM_burlapSackSetA_a1", -7.2f, -8.3f, 30);
            Metti("SM_barrelClosedA_a1", 1.5f, -8.8f);
            Metti("SM_basketA_a1", 6.5f, -8.4f);
            Metti("SM_crateA_a1", -13.2f, -9.2f, 15);
            Metti("SM_cartB_a1", -12.5f, -2f, 35);

            // Tempio: due bracieri ai lati della porta
            Metti("SM_Brasero_01a", -1f, 20.5f);
            Metti("SM_Brasero_01a", 5f, 20.5f);

            // Camposanto: croci di legno in file, il memoriale con le candele, recinto con l'ingresso a est
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 5; c++)
                    Metti((r + c) % 2 == 0 ? "SM_VikingCross_01b" : "SM_VikingCross_02b", -31f + c * 3f, 28f + r * 4f);
            Metti("SM_bigRockA_a1", -24.5f, 40.5f, 10);
            Metti("SM_candleA_a1", -23f, 38.6f);
            Metti("SM_candleB_a1", -25.8f, 38.7f);
            Metti("SM_candleC_a1", -24.4f, 38.4f);
            Recinto(o, "SM_fenceA_a1", 3.3f, Rect.MinMaxRect(-34, 24, -15, 44), lato: 1, varco: 34f);

            // Cortile del Balivo: pennone con lo stendardo, gabbia per i fermati, carretto; recinto con l'ingresso a nord
            Metti("SM_Flag_UV_SM_Flag_UV", 40, 5);
            Metti("SM_Cage_UV", 43, -4, 10);
            Metti("SM_WoodenCart_UV", 34, -5, 80);
            Recinto(o, "SM_fenceA_a1", 3.3f, Rect.MinMaxRect(30.5f, -7.5f, 46, 15.5f), lato: 2, varco: 38f, salta: 3);

            // Locanda: tavoli all'aperto sulla strada, botti accanto al muro, stalla sul retro con fieno e carro
            Metti("SM_Tableandchairs", 29, -21.6f);
            Metti("SM_Tableandchairs", 18.5f, -21.6f, 15);
            Metti("SM_WineBarrels_UV", 35.8f, -26.5f);
            Metti("SM_roofStablesTavern_a1", 25, -32.6f, 180);
            Metti("SM_HayPiles_01a", 21, -33);
            Metti("SM_HayStorage_01a", 28.5f, -33.4f);
            Metti("SM_bucketA_a1", 31, -32.8f);
            Metti("SM_cartB_a1", 39, -40, 60);

            // Gilda: bacheca degli incarichi davanti alla porta; campo d'addestramento con fantocci e bersagli
            Metti("SM_Garrison_Dummy", -40, 15.5f);
            Metti("SM_Garrison_Dummy", -36, 15.5f);
            Metti("SM_Garrison_Dummy", -32, 15.5f);
            Metti("SM_StrawArcheryTarget_01a", -44, 19.5f, 180);
            Metti("SM_StrawArcheryTarget_01a", -40.5f, 19.5f, 180);
            Metti("SM_WoodWeaponsStand_UV", -28.5f, 18.5f, 180);
            Metti("SM_ArcheryStorage_01a", -28, 14);

            // Fabbro: tettoia con la fornace, mola, rastrelliera per le pelli, armi, botte per temprare, legna
            Metti("SM_woodenCoverA_a1", 38.5f, 29);
            Metti("SM_furnaceA_a1", 38.5f, 29);
            Metti("SM_Blacksmith_ShapeningStone", 36, 33.5f, 20);
            Metti("SM_Blacksmith_TanningRack", 41.5f, 33.5f);
            Metti("SM_WoodWeaponsStand_UV", 41.5f, 25, 270);
            Metti("SM_barrelOpenedA_a1", 35.8f, 25.6f);
            Metti("SM_firewoodSetA_a1", 42, 37.5f, 90);

            // Orti comuni: tre file di ortaggi, recinto con l'ingresso a nord
            string[] ortaggi = { "SM_WildCarrot_M_01", "SM_WildCarrot_M_02", "SM_WildCarrot_S_01" };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 7; c++)
                    Metti(ortaggi[(r + c) % 3], -40f + c * 2.6f, -17.2f - r * 2.4f, c * 47f);
            Recinto(o, "SM_fenceB_a1", 1.7f, Rect.MinMaxRect(-42, -24, -22, -15), lato: 2, varco: -32f);

            // Fienile: fieno e carro; recinto del pascolo a sud con il cancello verso il fienile
            Metti("SM_HayPiles_01a", 64, -50);
            Metti("SM_HayPiles_01a", 65.5f, -52, 40);
            Metti("SM_HayStorage_01b", 47.5f, -50.5f);
            Metti("SM_cartB_a1", 57, -45.5f, 170);
            Recinto(o, "SM_fenceD_a1", 3.3f, Rect.MinMaxRect(46, -66, 70, -56), lato: 2, varco: 56f);

            // Mulino: sacchi di farina e un carretto davanti alla porta
            Metti("SM_burlapSackSetA_a1", -138.5f, 27.5f);
            Metti("SM_burlapSackA_a1", -138.8f, 18.5f, 60);
            Metti("SM_cartB_a1", -135.5f, 19.5f, 100);

            return o;
        }

        /// <summary>
        /// Un recinto lungo i quattro lati del rettangolo, a pezzi lunghi 'passo' metri (lungo il loro asse Z).
        /// Sul lato indicato (0 sud, 1 est, 2 nord, 3 ovest) resta un varco largo un pezzo intorno alla coordinata 'varco';
        /// 'salta' = un lato da non recintare (per esempio quello chiuso da un edificio), -1 = nessuno.
        /// </summary>
        static void Recinto(List<Oggetto> o, string prefab, float passo, Rect r, int lato, float varco, int salta = -1)
        {
            for (int l = 0; l < 4; l++)
            {
                if (l == salta) continue;
                bool orizzontale = l == 0 || l == 2;
                float inizio = orizzontale ? r.xMin : r.yMin, fine = orizzontale ? r.xMax : r.yMax;
                int pezzi = Mathf.Max(1, Mathf.RoundToInt((fine - inizio) / passo));
                float lungo = (fine - inizio) / pezzi;
                for (int k = 0; k < pezzi; k++)
                {
                    float centro = inizio + (k + 0.5f) * lungo;
                    if (l == lato && Mathf.Abs(centro - varco) < lungo * 0.6f) continue;
                    float fisso = l == 0 ? r.yMin : l == 2 ? r.yMax : l == 1 ? r.xMax : r.xMin;
                    if (orizzontale) o.Add(new Oggetto(prefab, centro, fisso, 90f));
                    else o.Add(new Oggetto(prefab, fisso, centro, 0f));
                }
            }
        }

        // ------------------------------------------------------------------ interni

        /// <summary>
        /// Un mobile o un oggetto dentro un edificio, in coordinate dell'edificio: x lungo la facciata (da 0 alla lunghezza),
        /// z verso il fondo (da 0 a -6, la porta sta a z = 0). piano 0 o 1; alzata = sopra un tavolo o una mensola (m);
        /// rotY rispetto all'edificio. Viene appoggiato al pavimento con il centro nel punto dato.
        /// </summary>
        public class Arredo
        {
            public readonly string prefab;
            public readonly float x, z, rotY, alzata;
            public readonly int piano;

            public Arredo(string prefab, float x, float z, float rotY = 0f, int piano = 0, float alzata = 0f)
            {
                this.prefab = prefab; this.x = x; this.z = z; this.rotY = rotY; this.piano = piano; this.alzata = alzata;
            }
        }

        /// <summary>Quanti moduli (da sinistra) hanno il pavimento al primo piano: gli altri restano a doppia altezza.</summary>
        public static int ModuliSoppalco(Edificio e) => e.piani < 2 || e.lotto == "tempio" ? 0 : Mathf.Max(1, e.moduli - 1);

        /// <summary>Gli arredi di un edificio (porta al centro della facciata: nel modulo moduli/2, da x + 0,45 a x + 1,55).</summary>
        public static List<Arredo> ArrediDi(Edificio e)
        {
            var a = new List<Arredo>();
            void M(string prefab, float x, float z, float rot = 0f, int piano = 0, float alzata = 0f) =>
                a.Add(new Arredo(prefab, x, z, rot, piano, alzata));

            switch (e.nome)
            {
                case "Tempio del Cuore":
                    // Sala unica a doppia altezza: altare di fronte alla porta, bracieri, panche per i fedeli
                    M("SM_Carpet_UV", 7, -2.8f);
                    M("SM_tableA_a1", 7, -5.1f, 90);                      // altare (il frammento del Cuore arriverà al 6.5)
                    M("SM_SilverCandle_UV", 6.3f, -5.1f, 0, 0, 0.8f);
                    M("SM_SilverCandle_UV", 7.7f, -5.1f, 0, 0, 0.8f);
                    M("SM_SilverCup_UV", 7f, -5.2f, 0, 0, 0.8f);
                    M("SM_Brasero_01a", 4.6f, -4.9f);
                    M("SM_Brasero_01a", 9.4f, -4.9f);
                    foreach (float bx in new[] { 2.5f, 12f, 15.5f })
                    {
                        M("SM_benchA_a1", bx, -2.2f, 90);
                        M("SM_benchA_a1", bx, -3.6f, 90);
                    }
                    M("SM_candleA_a1", 0.6f, -5.4f);
                    M("SM_candleC_a1", 17.4f, -5.4f);
                    break;

                case "Locanda":
                    // Piano terra: tavoli e camino a sinistra, bancone e botti nel modulo a doppia altezza a destra
                    M("SM_Tableandchairs", 4.2f, -4.2f);
                    M("SM_Tableandchairs", 10f, -2.4f, 20);
                    M("SM_Fireplace", 0.8f, -2.2f);
                    M("SM_firewoodC_a1", 1.2f, -4.4f);
                    M("SM_tableA_a1", 15.5f, -2.5f);                       // bancone, due tavoli in fila
                    M("SM_tableA_a1", 15.5f, -4.6f);
                    M("SM_mugA_a1", 15.4f, -2.0f, 0, 0, 0.8f);
                    M("SM_jugA_a1", 15.6f, -3.1f, 0, 0, 0.8f);
                    M("SM_plateA_a1", 15.5f, -4.4f, 0, 0, 0.8f);
                    M("SM_WineBarrels_UV", 17.1f, -3.5f);
                    M("SM_barrelClosedA_a1", 13f, -5.3f);
                    M("SM_barrelOpenedA_a1", 13.9f, -5.4f);
                    M("SM_crateA_a1", 16.9f, -0.9f);
                    // Primo piano (moduli 0 e 1): tre camere, le future stanze in affitto
                    foreach (float lx in new[] { 2f, 6.2f, 10.2f })
                    {
                        M("SM_Castle_BED_2", lx, -4.9f, 0, 1);
                        M("SM_chestA_a1", lx, -2.8f, 90, 1);
                    }
                    M("SM_candleB_a1", 4f, -5.5f, 0, 1);
                    break;

                case "Gilda degli avventurieri":
                    // Piano terra: banco del gildiere, scaffale, casse; tavolo per gli avventurieri
                    M("SM_tableA_a1", 9.5f, -3.4f, 90);
                    M("SM_chairA_a1", 9.5f, -4.4f, 180);
                    M("SM_Bookshelf", 9.5f, -5.5f);
                    M("SM_candleA_a1", 9f, -3.4f, 0, 0, 0.8f);
                    M("SM_crateA_a1", 11.2f, -5.2f);
                    M("SM_crateA_a1", 11.3f, -4.2f, 25);
                    M("SM_chestA_a1", 11.2f, -1.4f);
                    M("SM_Tableandchairs", 2.8f, -3.2f);
                    M("SM_WoodWeaponsStand_UV", 0.5f, -5.2f);
                    // Primo piano: archivio degli incarichi
                    M("SM_Bookshelf2", 0.9f, -3f, 0, 1);
                    M("SM_tableA_a1", 3.5f, -4f, 0, 1);
                    M("SM_chairA_a1", 4.3f, -4f, 270, 1);
                    M("SM_chestA_a1", 3.5f, -1.2f, 90, 1);
                    break;

                case "Casa del Balivo":
                    // Piano terra: sala delle udienze nel modulo a doppia altezza, camino e sedie nell'altro
                    M("SM_Carpet_UV", 9f, -3f);
                    M("SM_tableA_a1", 9f, -3.8f, 90);
                    M("SM_Castle_Chair_Var1", 9f, -4.9f, 180);
                    M("SM_chairA_a1", 8.4f, -2.6f);
                    M("SM_chairA_a1", 9.6f, -2.6f);
                    M("SM_candleA_a1", 8.5f, -3.8f, 0, 0, 0.8f);
                    M("SM_bigBottleA_a1", 9.6f, -3.9f, 0, 0, 0.8f);
                    M("SM_Bookshelf", 10.5f, -5.6f);
                    M("SM_Fireplace2", 0.8f, -3f);
                    M("SM_Castle_Chair_Var2", 2.6f, -2.4f, 250);
                    M("SM_chestA_a1", 3.5f, -5.2f, 90);
                    // Primo piano: la camera del Balivo
                    M("SM_Castle_BED_2", 2.2f, -4.9f, 0, 1);
                    M("SM_Carpet_02", 2.2f, -2.6f, 0, 1);
                    M("SM_chestA_a1", 4.6f, -5.2f, 90, 1);
                    M("SM_candleB_a1", 0.6f, -5.5f, 0, 1);
                    break;

                case "Casa del Balivo, ala":
                    // Archivio della Corona: scaffali, casse, un tavolo da scrivano
                    M("SM_woodenShelfA_a1", 3f, -5.3f);
                    M("SM_tableA_a1", 4.3f, -2.6f);
                    M("SM_chairA_a1", 3.5f, -2.6f, 90);
                    M("SM_candleA_a1", 4.3f, -2.2f, 0, 0, 0.8f);
                    M("SM_crateA_a1", 5.3f, -4.6f);
                    M("SM_chestA_a1", 0.7f, -3.5f);
                    break;

                case "Bottega del fabbro":
                    M("SM_woodenShelfC_a1", 3.5f, -5.4f);
                    M("SM_tableA_a1", 4f, -2.5f, 90);
                    M("SM_ToolHammer_01a", 3.6f, -2.5f, 0, 0, 0.8f);
                    M("SM_ToolTong_01a", 4.4f, -2.4f, 30, 0, 0.8f);
                    M("SM_crateA_a1", 5.3f, -4.2f);
                    M("SM_barrelClosedA_a1", 5.3f, -1f);
                    M("SM_ToolShovel_01a", 0.5f, -4.5f);
                    break;

                case "Mulino":
                    M("SM_burlapSackSetA_a1", 4f, -4.6f);
                    M("SM_FoodBagPack_01a", 4.4f, -2.4f, 90);
                    M("SM_FoodBagSeed_03a", 1f, -3.8f);
                    M("SM_barrelClosedA_a1", 5.3f, -1f);
                    M("SM_woodenShelfC_a1", 1.6f, -5.4f);
                    M("SM_bucketB_a1", 2.6f, -3.2f);
                    break;

                case "Fienile":
                    M("SM_HayPiles_01a", 2f, -4.5f);
                    M("SM_HayPiles_01a", 4f, -4.8f, 60);
                    M("SM_HayStorage_01a", 10f, -5f);
                    M("SM_HayStorage_01b", 10.8f, -4.2f, 30);
                    M("SM_ToolFork_01a", 11.5f, -1.2f);
                    M("SM_ToolShovel_01a", 11.3f, -2f, 20);
                    M("SM_WoodenCart_UV", 3f, -2.2f, 90);
                    break;

                default:
                    if (e.lotto.StartsWith("casa")) ArrediCasa(e, M);
                    break;
            }
            return a;
        }

        /// <summary>Una casa dei coloni: letto, camino, tavolo con panca, cassa, tappeto; al primo piano la camera.</summary>
        static void ArrediCasa(Edificio e, System.Action<string, float, float, float, int, float> M)
        {
            if (e.moduli == 1)
            {
                M("SM_Castle_BED_2", 4.3f, -4.9f, 0, 0, 0);
                M("SM_tableA_a1", 4.5f, -1.9f, 90, 0, 0);
                M("SM_benchA_a1", 4.5f, -0.95f, 90, 0, 0);
                M("SM_fireplaceA_a1", 0.4f, -4.1f, 0, 0, 0);
                M("SM_chestA_a1", 5.3f, -3.2f, 0, 0, 0);
                M("SM_Carpet_02", 2.4f, -3.2f, 0, 0, 0);
                M("SM_jugB_a1", 4.2f, -1.9f, 0, 0, 0.8f);
                M("SM_plateB_a1", 4.8f, -1.8f, 0, 0, 0.8f);
                return;
            }
            bool dueP = e.piani >= 2;
            // Piano terra: cucina e tavolo nel modulo della porta; letto (o dispensa, se c'è il primo piano) nell'altro
            M("SM_fireplaceA_a1", 0.4f, -2f, 0, 0, 0);
            M("SM_tableA_a1", 9.5f, -2.5f, 90, 0, 0);
            M("SM_benchA_a1", 9.5f, -1.6f, 90, 0, 0);
            M("SM_benchA_a1", 9.5f, -3.4f, 90, 0, 0);
            M("SM_mugA_a1", 9.2f, -2.5f, 0, 0, 0.8f);
            M("SM_potB_a1", 11.2f, -0.9f, 0, 0, 0);
            M("SM_woodenShelfB_a1", 9.5f, -5.6f, 0, 0, 0);
            M("SM_Carpet_02", 3f, -3f, 0, 0, 0);
            if (dueP)
            {
                M("SM_crateA_a1", 2.2f, -5.2f, 0, 0, 0);
                M("SM_barrelClosedA_a1", 3.3f, -5.3f, 0, 0, 0);
                M("SM_Castle_BED_2", 2.2f, -4.9f, 0, 1, 0);
                M("SM_chestA_a1", 4.6f, -5.2f, 90, 1, 0);
                M("SM_Carpet_02", 2.4f, -2.4f, 0, 1, 0);
            }
            else
            {
                M("SM_Castle_BED_2", 2.2f, -4.9f, 0, 0, 0);
                M("SM_chestA_a1", 5f, -5.2f, 90, 0, 0);
            }
        }

        /// <summary>Strade e sentieri dentro il villaggio: larghezza in metri e punti.</summary>
        public static readonly (float larghezza, Vector2[] punti)[] Strade =
        {
            // Le strade del villaggio si innestano su quelle della valle (ValleMappa.Strade) nel primo punto o nell'ultimo.
            (5f, new[] { new Vector2(130, -78), new Vector2(80, -48), new Vector2(52, -31), new Vector2(46, -18), new Vector2(16, -14) }),  // dal passo alla piazza
            (5f, new[] { new Vector2(14, 14), new Vector2(22, 40), new Vector2(26, 70), new Vector2(30, 125), new Vector2(29.5f, 135) }),    // verso il ponte
            (4.5f, new[] { new Vector2(21, 19), new Vector2(60, 19), new Vector2(100, 31.8f), new Vector2(140, 53.6f) }),                     // verso la grotta est
            (3f, new[] { new Vector2(-16, -11), new Vector2(-48, -11), new Vector2(-100, 0), new Vector2(-139, 23) }),                         // sentiero del mulino
            (2.5f, new[] { new Vector2(-6, -12), new Vector2(-6, -46) }),                                                                     // viottolo delle case
            // Raccordi del ponte (a 250 m verso nord): dalla strada del sud alla testa est, dalla testa ovest alle strade
            // della collina e delle rovine
            (4f, new[] { new Vector2(26.1f, 210), new Vector2(32, 234), new Vector2(34.7f, 247.6f), new Vector2(31.8f, 248.3f) }),
            (4f, new[] { new Vector2(18.2f, 251.7f), new Vector2(15.3f, 252.4f), new Vector2(15, 265), new Vector2(5.2f, 282.4f) }),
            (4f, new[] { new Vector2(15.3f, 252.4f), new Vector2(5, 255), new Vector2(-14.9f, 256.9f) }),
        };

        /// <summary>Dove arriva nella valle chi ha appena firmato il Registro: davanti alla porta del tempio, sulla piazza.</summary>
        public static readonly Vector2 ArrivoTempio = new Vector2(2f, 20f);

        public static Lotto TrovaLotto(string id)
        {
            foreach (Lotto l in Lotti) if (l.id == id) return l;
            return null;
        }

        /// <summary>Da coordinate del villaggio a coordinate del mondo (x = est, z = nord).</summary>
        public static Vector2 AlMondo(Vector2 locale) => ValleMappa.Villaggio + locale;
    }
}