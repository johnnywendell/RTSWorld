Shader "RTSWorld/Environment Colors"
{
    Properties { _SceneShadows("Sombras na cena de estilo", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _SceneShadows;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float4 color:COLOR; float3 positionWS:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 normal=normalize(input.normalWS);
                Light light=GetMainLight();
                half shadow = _SceneShadows > 0.5
                    ? GetMainLight(TransformWorldToShadowCoord(input.positionWS)).shadowAttenuation : 1;
                half3 illumination=max(SampleSH(normal)*0.5,half3(0.18,0.18,0.18))+light.color*0.5*shadow*saturate(dot(normal,light.direction));
                if (input.color.a < .5)
                {
                    float2 p = input.positionWS.xz;
                    half wave = sin(dot(p, float2(.29,.63)) + _Time.y*.85) *
                                sin(dot(p, float2(.51,-.17)) - _Time.y*.62);
                    half shore = 1 - smoothstep(.006, .045, input.color.a);
                    half foam = shore * smoothstep(.53, .93, wave * .5 + .5);
                    half glint = pow(saturate(1-dot(normalize(GetWorldSpaceViewDir(input.positionWS)),normal)),3);
                    half3 water = input.color.rgb * (.58 + wave*.038 + glint*.15);
                    return half4(water + foam*half3(.12,.16,.13), 1);
                }
                if (input.color.a < .95)
                {
                    float2 p = input.positionWS.xz;
                    half detail = sin(p.x*1.83 + sin(p.y*.72))*sin(p.y*1.32 - sin(p.x*.61));
                    half exposure = smoothstep(.14,.52,1-normal.y);
                    half3 earth = lerp(input.color.rgb, half3(.49,.43,.36), exposure*.64);
                    return half4(earth * illumination * (1 + detail*.045), 1);
                }
                return half4(input.color.rgb*illumination,1);
            }
            ENDHLSL
        }
    }
}
