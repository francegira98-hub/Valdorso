// Il sigillo della Corona: un velo di luce che attraversa la gola del passo.
// Nebbia chiara che sale dal suolo e sfuma verso l'alto, rune dorate che compaiono, salgono piano e svaniscono,
// tutto che pulsa con il battito del Cuore (due colpi e una pausa). Di notte è più forte (_Notte, da 0 a 1).
// Le rune sono disegnate dal calcolo, senza texture: ogni cella da 1,2 m ha un segno diverso fatto di tre tratti.
Shader "Valdorso/Sigillo"
{
    Properties
    {
        _ColoreRune ("Colore delle rune", Color) = (1.0, 0.78, 0.32, 1)
        _ColoreVelo ("Colore del velo", Color) = (0.85, 0.88, 1.0, 1)
        _Opacita ("Opacità del velo", Range(0, 1)) = 0.55
        _Intensita ("Intensità delle rune", Range(0, 8)) = 3.5
        _Rune ("Quante rune (0 nessuna, 1 tutte)", Range(0, 1)) = 0.22
        _Cella ("Lato delle rune (m)", Float) = 0.9
        _Salita ("Velocità con cui salgono (m/s)", Float) = 0.25
        _Notte ("Notte (0 giorno, 1 notte)", Range(0, 1)) = 0
        _Tocco ("Lampo quando qualcuno lo tocca", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha   // premoltiplicato: il velo copre, le rune fanno luce
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Sigillo"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColoreRune;
                half4 _ColoreVelo;
                half _Opacita;
                half _Intensita;
                half _Rune;
                float _Cella;
                float _Salita;
                half _Notte;
                half _Tocco;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;   // x: metri lungo il velo, y: metri dal suolo, z: 0 al suolo e 1 in cima
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = i.uv.xyz;
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Rumore(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Distanza di un punto da un tratto
            float Tratto(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // Uno dei 9 punti di una griglia 3x3 dentro la cella
            float2 Punto(float n)
            {
                n = floor(fmod(n, 9.0));
                return float2(fmod(n, 3.0), floor(n / 3.0)) * 0.3 + 0.2;
            }

            // Il battito del Cuore: due colpi e una pausa
            float Battito(float t)
            {
                float f = frac(t / 1.6);
                return exp(-pow((f - 0.05) * 16.0, 2.0)) + 0.65 * exp(-pow((f - 0.24) * 16.0, 2.0));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float battito = Battito(t);
                float notte = _Notte;

                // --- Il velo: nebbia che sale dal suolo, più fitta in basso, che scorre piano
                float fondo = 1.0 - smoothstep(0.0, 1.0, i.uv.z);
                float onde = Rumore(float2(i.uv.x * 0.12, i.uv.y * 0.18 - t * 0.12)) * 0.6
                           + Rumore(float2(i.uv.x * 0.35 + t * 0.05, i.uv.y * 0.4)) * 0.4;
                float velo = saturate(_Opacita * (0.35 + 0.65 * fondo) * (0.6 + 0.8 * onde));
                velo *= 1.0 - smoothstep(0.85, 1.0, i.uv.z); // sfuma verso la cima
                velo *= smoothstep(0.0, 0.03, i.uv.z);        // e al suolo non ha uno spigolo netto

                // --- Tende di luce verticali, come un'aurora, più fitte in basso
                float tende = pow(Rumore(float2(i.uv.x * 0.6, t * 0.15)), 3.0) * 1.5 * (0.4 + 0.6 * fondo);

                // --- Le rune: solo in alcune fasce orizzontali (come anelli incisi nel velo), che salgono piano
                float2 p = float2(i.uv.x, i.uv.y - t * _Salita) / _Cella;
                float2 cella = floor(p);
                float2 dentro = frac(p);
                float fascia = step(0.72, Hash(float2(7.3, floor(cella.y / 2.0))));   // una fascia ogni tanto
                float h = Hash(cella);
                float d = 1.0;
                d = min(d, Tratto(dentro, Punto(h * 97.0), Punto(h * 53.0 + 1.0)));
                d = min(d, Tratto(dentro, Punto(h * 31.0 + 2.0), Punto(h * 71.0 + 4.0)));
                d = min(d, Tratto(dentro, Punto(h * 13.0 + 5.0), Punto(h * 89.0 + 7.0)));
                float segno = (1.0 - smoothstep(0.04, 0.075, d)) + (1.0 - smoothstep(0.04, 0.22, d)) * 0.4; // tratto e alone
                float accesa = step(1.0 - _Rune * 3.0, h) * fascia * saturate(sin(t * 0.7 + h * 40.0) * 1.5 + 0.2);
                float rune = segno * accesa * (1.0 - smoothstep(0.55, 0.8, i.uv.z)) * smoothstep(0.02, 0.1, i.uv.z);

                float forza = _Intensita * (0.55 + 0.45 * notte) * (0.7 + 0.6 * battito) + _Tocco * 4.0;
                float luce = (0.9 + 0.5 * notte + 0.4 * battito);
                // Colore premoltiplicato: il velo è una parete chiara che copre, tende e rune sono luce che si somma
                float3 colore = _ColoreVelo.rgb * luce * (velo + tende * velo)
                              + _ColoreRune.rgb * rune * forza;
                // Le rune coprono anche un po' il velo dietro, così restano leggibili di giorno sulla nebbia chiara
                float alfa = saturate(velo * (1.0 + tende * 0.5) + rune * 0.55 + _Tocco * 0.3 * fondo);

                colore = MixFog(colore, i.fogCoord);
                return half4(colore, alfa);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
