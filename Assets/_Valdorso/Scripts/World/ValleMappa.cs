using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// La mappa della valle in numeri (approvata da Fra il 28/09).
    /// Tutte le misure sono in metri. Ogni punto è un Vector2 con x = est, y = nord
    /// (nel mondo di Unity il nord è l'asse Z). L'angolo sud-ovest della valle è (0, 0).
    /// Serve allo strumento che crea il terreno e, più avanti, alla mappa di gioco (tasto M).
    /// Per spostare un luogo basta cambiare qui i suoi numeri e ricostruire la valle.
    /// </summary>
    public static class ValleMappa
    {
        public const float Lato = 1500f;        // la valle è 1,5 × 1,5 km (3 × 3 tessere)
        public const float LatoTessera = 500f;  // ogni tessera è 500 × 500 m
        public const float Anello = 500f;       // tutto intorno, un anello di montagne largo una tessera
        public const float Origine = -Anello;   // il terreno va da -500 a 2000 m
        public const int TesserePerLato = 5;    // 5 × 5 tessere in tutto

        static Vector2 V(float est, float nord) => new Vector2(est, nord);

        /// <summary>
        /// Il contorno della conca: fuori da qui salgono i monti. A sud-est il corridoio del passo
        /// attraversa l'anello di montagne fino al bordo del terreno, verso Aurelia.
        /// </summary>
        public static readonly Vector2[] Valle =
        {
            V(100, 1125), V(137.5f, 1250), V(400, 1290), V(725, 1315), V(1050, 1280), V(1305, 1240),
            V(1375, 1050), V(1395, 725), V(1380, 375), V(1400, 200), V(1500, 135), V(1750, 150), V(2000, 160),
            V(2000, 30), V(1750, 45), V(1500, 50), V(1380, 95), V(1200, 95), V(900, 80), V(550, 90), V(250, 110), V(100, 195),
            V(75, 500), V(87.5f, 850)
        };

        /// <summary>Il bosco grande a ovest e nord-ovest, con dentro le rovine.</summary>
        public static readonly Vector2[] Bosco =
        {
            V(112, 1220), V(350, 1255), V(525, 1212), V(500, 1050), V(465, 850),
            V(500, 675), V(435, 500), V(300, 420), V(140, 450), V(100, 800)
        };

        /// <summary>
        /// Il fiume come curva morbida (Bézier): punto, controllo, controllo, punto, ...
        /// Nasce nei monti di nord-est e finisce nel lago a sud-ovest.
        /// </summary>
        public static readonly Vector2[] FiumeControlli =
        {
            V(1180, 1400), V(1150, 1225), V(1075, 1125), V(1030, 1000),
            V(985, 875), V(900, 800), V(875, 700),
            V(850, 600), V(730, 500), V(655, 450),
            V(580, 400), V(480, 375), V(430, 335)
        };

        /// <summary>Le strade di terra battuta.</summary>
        public static readonly Vector2[][] Strade =
        {
            new[] { V(2000, 95), V(1750, 97), V(1500, 85), V(1375, 145), V(1150, 270), V(980, 372) },   // dal passo all'ingresso del villaggio
            new[] { V(879.5f, 585), V(875, 685) },                                        // dall'uscita nord del villaggio al ponte
            new[] { V(865, 715), V(800, 830), V(680, 955) },                              // dal ponte alla collina
            new[] { V(855, 705), V(650, 725), V(500, 790), V(345, 825) },                 // dal ponte alle rovine
            new[] { V(990, 503.6f), V(1075, 550), V(1250, 725), V(1350, 790) },           // dall'uscita est del villaggio alla grotta est
            new[] { V(660, 1075), V(680, 1150), V(705, 1230), V(730, 1265) }              // sentiero verso la grotta nord
        };

        // Forme tonde: centro e raggi (est-ovest, nord-sud).
        public static readonly Vector2 Collina = V(650, 1037), CollinaRaggi = V(155, 115);
        public static readonly Vector2 Lago = V(330, 275), LagoRaggi = V(150, 100);
        public static readonly Vector2 Boschetto = V(1262, 930), BoschettoRaggi = V(95, 70);
        public static readonly Vector2 Pascoli = V(1100, 263), PascoliRaggi = V(120, 70);
        public static readonly Vector2 Villaggio = V(850, 450);

        /// <summary>I luoghi da segnare nella scena (diventano segnaposti per il lavoro dei passi dopo).</summary>
        public static readonly (string nome, Vector2 posizione)[] Luoghi =
        {
            ("Arrivo_Tempio", V(852, 470)),
            ("Centro_Villaggio", V(850, 450)),
            ("Mulino", V(682, 473)),
            ("Ponte", V(875, 700)),
            ("Cima_Collina_Orso", V(650, 1037)),
            ("Rovine_Antichi", V(292, 845)),
            ("Ingresso_Cripta", V(340, 820)),
            ("Ingresso_GrottaEst", V(1350, 790)),
            ("Ingresso_GrottaNord", V(730, 1265)),
            ("Passo_Aurelia", V(1750, 97)),
            ("Pascoli", V(1100, 263)),
            ("Lago", V(330, 275))
        };

        /// <summary>Il fiume come fila di punti, dalla sorgente al lago.</summary>
        public static List<Vector2> CampionaFiume(int puntiPerTratto)
        {
            var punti = new List<Vector2> { FiumeControlli[0] };
            for (int i = 0; i + 3 < FiumeControlli.Length; i += 3)
            {
                for (int k = 1; k <= puntiPerTratto; k++)
                {
                    float t = k / (float)puntiPerTratto;
                    punti.Add(Bezier(FiumeControlli[i], FiumeControlli[i + 1], FiumeControlli[i + 2], FiumeControlli[i + 3], t));
                }
            }
            return punti;
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}