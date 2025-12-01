Shader "Hidden/OverseerGlitch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ChromaticIntensity ("Chromatic Aberration Intensity", Range(0, 1)) = 0
        _DistortionAmount ("Distortion Amount", Range(0, 0.1)) = 0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0
        _GlitchColor ("Glitch Color", Color) = (1, 0, 0, 0.1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ChromaticIntensity;
            float _DistortionAmount;
            float _ScanlineIntensity;
            float _FlickerIntensity;
            float _NoiseIntensity;
            float4 _GlitchColor;

            // Random function
            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // Noise function
            float noise(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);
                
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                
                // Screen distortion
                if (_DistortionAmount > 0)
                {
                    float distortTime = _Time.y * 10;
                    float distortNoise = noise(float2(uv.y * 50 + distortTime, distortTime));
                    uv.x += (distortNoise - 0.5) * _DistortionAmount;
                }
                
                // Sample with chromatic aberration
                float3 col;
                if (_ChromaticIntensity > 0)
                {
                    float2 offset = _ChromaticIntensity * 0.01;
                    col.r = tex2D(_MainTex, uv + float2(offset.x, 0)).r;
                    col.g = tex2D(_MainTex, uv).g;
                    col.b = tex2D(_MainTex, uv - float2(offset.x, 0)).b;
                }
                else
                {
                    col = tex2D(_MainTex, uv).rgb;
                }
                
                // Scanlines
                if (_ScanlineIntensity > 0)
                {
                    float scanline = sin(uv.y * 300 + _Time.y * 20) * 0.5 + 0.5;
                    col *= 1.0 - (scanline * _ScanlineIntensity * 0.3);
                }
                
                // Static noise
                if (_NoiseIntensity > 0)
                {
                    float noiseVal = random(uv + frac(_Time.y));
                    col = lerp(col, float3(noiseVal, noiseVal, noiseVal), _NoiseIntensity * 0.3);
                }
                
                // Flicker
                if (_FlickerIntensity > 0)
                {
                    float flicker = random(float2(_Time.y, 0));
                    col *= 1.0 - (flicker * _FlickerIntensity);
                }
                
                // Color shift
                if (_GlitchColor.a > 0)
                {
                    col = lerp(col, _GlitchColor.rgb, _GlitchColor.a);
                }
                
                // Horizontal glitch lines (random)
                float glitchLine = step(0.99, random(float2(floor(uv.y * 100), floor(_Time.y * 20))));
                if (glitchLine > 0 && _DistortionAmount > 0)
                {
                    col = tex2D(_MainTex, uv + float2(0.05, 0)).rgb;
                }
                
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
