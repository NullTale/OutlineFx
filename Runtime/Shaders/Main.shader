Shader "Hidden/OutlineFx/Main"
{
    SubShader
    {
		Pass	// 0
        {
            name "Transparent"
		        
		    Cull Off
		    ZWrite Off
		    ZTest LEqual
        	Blend SrcAlpha OneMinusSrcAlpha
        	
            HLSLPROGRAM
            
            #include "Utils.hlsl"
            
            #pragma vertex vert_mesh
            #pragma fragment frag
            
            sampler2D _MainTex;
			float4    _Color;
            float     _Alpha;
            
            // =======================================================================
            float4 frag(fragIn i) : SV_Target
            {
            	if (tex2D(_MainTex, i.uv).a < _Alpha)
            		discard;
            	
            	return _Color;
            }
            ENDHLSL
        }
    	
        Pass	// 1
        {
            name "Outline"
        	
		    Cull Off
		    ZWrite Off
		    ZTest Off
        	Blend SrcAlpha OneMinusSrcAlpha
        	
            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_screen
            #pragma fragment frag
            
            #pragma multi_compile_local BOX CROSS
            #pragma multi_compile_local SOFT HARD
            #pragma multi_compile_local _ ALPHA_MASK
            
			#define	BLUR_LENGTH 9
			#define	BLUR_LENGTH_HALF ((BLUR_LENGTH - 1) / 2)
			static const float	k_BlurWeights[BLUR_LENGTH] =
			{
				0.046995 * 2,
				0.064759 * 2,
				0.120985 * 2,
				0.176033 * 2,
				0.199471 * 2,
				0.176033 * 2,
				0.120985 * 2,
				0.064759 * 2,
				0.046995 * 2,
			};

            sampler2D _MainTex;
            sampler2D _AlphaTex;
			float4    _AlphaTO;
			float2    _Step;
			float     _Solid;

            // =======================================================================
            float4 _sample_soft(float2 uv, in const float2 step)
            {
				float4 result = 0;
				uv -= BLUR_LENGTH_HALF * step;
            	
            	[unroll]
				for (int n = 0; n < BLUR_LENGTH; n ++)
				{
					result += tex2D(_MainTex, uv) * k_BlurWeights[n];
					uv += step;
				}
            	
            	return result;
            }
            
            float4 _sample_hard(float2 uv, in const float2 step)
            {
				float4 result = 0;
            	
				uv -= BLUR_LENGTH_HALF * step;
            	
            	[unroll]
				for (int n = 0; n < BLUR_LENGTH; n ++)
				{
					float4 sample = tex2D(_MainTex, uv);
					result = max(sample, result);
					uv += step;
				}
            	
            	return result;
            }
            
            float4 _sample(const float2 uv, in const float2 step)
            {
#ifdef SOFT
            	return _sample_soft(uv, step);
#endif
#ifdef HARD
            	return _sample_hard(uv, step);
#endif
            }
            
            float4 frag(fragIn i) : SV_Target
            {
            	float4 color = tex2D(_MainTex, i.uv);
            	if (color.a > .0)
            	{
#ifdef ALPHA_MASK
            		return float4(color.xyz, color.a * _Solid * tex2D(_AlphaTex, mad(i.uv, _AlphaTO.xy, _AlphaTO.zw)).a);
#endif
            		return float4(color.xyz, color.a * _Solid);
            	}
            	
				float4 result = 0;

#ifdef BOX
				const float2 stepX = float2(_Step.x, 0);
				const float2 stepY = float2(0, _Step.y);
				float2 uv = i.uv - BLUR_LENGTH_HALF * stepX;
            	
            	[unroll]
				for (int n = 0; n < BLUR_LENGTH; n ++)
				{
#ifdef SOFT
					result += _sample(uv, stepY) * k_BlurWeights[n];
#endif
#ifdef HARD
					result = max(result, _sample(uv, stepY));
#endif
					
					uv += stepX;
				}
#endif            	
#ifdef CROSS
				result = (_sample(i.uv, _Step) + _sample(i.uv, float2(_Step.x, -_Step.y))) * .5f;
#endif
            	
            	return result;
            }
            ENDHLSL
        }
    	
    	Pass	// 2
        {
            name "Overlay"
        	
		    Cull Off
		    ZWrite Off
		    ZTest Off
        	Blend SrcAlpha OneMinusSrcAlpha
        	
            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_screen
            #pragma fragment frag
            
            sampler2D _MainTex;

            // =======================================================================
            float4 frag(fragIn i) : SV_Target
            {
            	return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}