// Céu do Sinal da Lia: gradiente (topo / horizonte / chão) + nuvens procedurais + sol, lua e estrelas.
// O DayNightController muda as cores por fase. A cor do HORIZONTE é a mesma da neblina:
// assim a cidade "some" no céu sem aquele corte entre o chão cinza e o céu laranja.
Shader "SinalDaLia/Ceu"
{
    Properties
    {
        _CorTopo ("Cor do topo", Color) = (0.36, 0.42, 0.5, 1)
        _CorHorizonte ("Cor do horizonte (= neblina)", Color) = (0.66, 0.66, 0.63, 1)
        _CorChao ("Cor abaixo do horizonte", Color) = (0.32, 0.31, 0.3, 1)
        _Curva ("Altura da faixa do horizonte", Range(1, 8)) = 3
        _SolDir ("Direção do sol", Vector) = (0.3, 0.6, 0.7, 0)
        _SolCor ("Cor do sol", Color) = (1, 0.92, 0.8, 1)
        _SolTamanho ("Tamanho do sol (rad)", Range(0, 0.2)) = 0.035
        _SolBrilho ("Brilho em volta do sol", Range(0, 4)) = 0.5
        _LuaDir ("Direção da lua", Vector) = (-0.3, 0.5, -0.8, 0)
        _LuaCor ("Cor da lua", Color) = (0.85, 0.9, 1, 1)
        _LuaTamanho ("Tamanho da lua (rad)", Range(0, 0.1)) = 0
        _NuvemCor ("Cor das nuvens (luz)", Color) = (0.72, 0.72, 0.72, 1)
        _NuvemSombra ("Cor das nuvens (sombra)", Color) = (0.45, 0.47, 0.5, 1)
        _Nuvens ("Cobertura das nuvens", Range(0, 1)) = 0.6
        _NuvemEscala ("Escala das nuvens", Float) = 1.4
        _NuvemVelocidade ("Velocidade das nuvens", Float) = 0.006
        _Estrelas ("Estrelas", Range(0, 1)) = 0
        _Exposicao ("Exposição", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Ceu"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CorTopo;
                float4 _CorHorizonte;
                float4 _CorChao;
                float4 _SolDir;
                float4 _SolCor;
                float4 _LuaDir;
                float4 _LuaCor;
                float4 _NuvemCor;
                float4 _NuvemSombra;
                float _Curva;
                float _SolTamanho;
                float _SolBrilho;
                float _LuaTamanho;
                float _Nuvens;
                float _NuvemEscala;
                float _NuvemVelocidade;
                float _Estrelas;
                float _Exposicao;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Hash31(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float s = 0.0;
                float a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    s += a * Ruido(p);
                    p = p * 2.03 + float2(17.1, 9.7);
                    a *= 0.5;
                }
                return s;
            }

            // disco (0..1) + brilho em volta de um astro na direção "astro"
            float Disco(float3 d, float3 astro, float tamanho)
            {
                float ang = acos(clamp(dot(d, astro), -1.0, 1.0));
                return 1.0 - smoothstep(tamanho * 0.8, tamanho, ang);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                // gradiente: o horizonte fica numa faixa baixa e o topo domina o resto do céu
                float kCima = 1.0 - pow(saturate(1.0 - y), _Curva);
                float kBaixo = saturate(-y * 5.0);
                float3 cor = y >= 0.0 ? lerp(_CorHorizonte.rgb, _CorTopo.rgb, kCima) : lerp(_CorHorizonte.rgb, _CorChao.rgb, kBaixo);
                float acima = saturate(y * 8.0 + 0.15);

                // sol: brilho largo (pôr do sol) + disco
                float3 sol = normalize(_SolDir.xyz);
                float cosSol = saturate(dot(d, sol));
                float brilho = (pow(cosSol, 6.0) * 0.35 + pow(cosSol, 48.0) * 0.8) * _SolBrilho;

                // nuvens projetadas num "teto" (mais densas e achatadas perto do horizonte)
                float2 uv = d.xz / (max(y, 0.0) + 0.12) * (_NuvemEscala * 0.35);
                uv += _Time.y * _NuvemVelocidade * float2(1.0, 0.35);
                float n = Fbm(uv);
                float limiar = 1.0 - _Nuvens;
                float nuvem = smoothstep(limiar - 0.08, limiar + 0.28, n) * saturate(y * 7.0) * step(0.0001, _Nuvens);
                float luzNuvem = saturate((n - limiar) * 2.2);
                float3 corNuvem = lerp(_NuvemSombra.rgb, _NuvemCor.rgb, luzNuvem);
                corNuvem += _SolCor.rgb * pow(cosSol, 5.0) * 0.45 * _SolBrilho * (1.0 - luzNuvem * 0.5);

                cor += _SolCor.rgb * brilho * acima;
                cor = lerp(cor, corNuvem, nuvem * 0.92);

                // estrelas (piscando) só no céu limpo
                if (_Estrelas > 0.001)
                {
                    float3 p = d * 260.0;
                    float3 celula = floor(p);
                    float h = Hash31(celula);
                    float3 centro = celula + 0.5 + (float3(Hash31(celula + 1.7), Hash31(celula + 4.1), Hash31(celula + 8.3)) - 0.5) * 0.6;
                    float ponto = saturate(1.0 - length(p - centro) * 2.2);
                    float estrela = step(0.985, h) * ponto * ponto;
                    float pisca = 0.65 + 0.35 * sin(_Time.y * (2.0 + h * 5.0) + h * 60.0);
                    cor += float3(0.85, 0.9, 1.0) * estrela * pisca * _Estrelas * 2.2 * saturate(y * 4.0) * (1.0 - nuvem);
                }

                // discos do sol e da lua (as nuvens tapam quase tudo)
                cor += _SolCor.rgb * Disco(d, sol, _SolTamanho) * 3.0 * (1.0 - nuvem * 0.85) * acima * step(0.0001, _SolTamanho);
                float3 lua = normalize(_LuaDir.xyz);
                float discoLua = Disco(d, lua, _LuaTamanho) * step(0.0001, _LuaTamanho);
                float cosLua = saturate(dot(d, lua));
                cor += _LuaCor.rgb * (discoLua * 1.6 + pow(cosLua, 40.0) * 0.12 * step(0.0001, _LuaTamanho)) * (1.0 - nuvem * 0.7) * acima;

                return float4(cor * _Exposicao, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
