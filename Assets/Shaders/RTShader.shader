Shader "Custom/RTShader"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
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
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            /////////////////
            // CONSTS
            /////////////////
            static const int CheckerPattern = 1;
            static const int InvisibleLightSource = 2;
            static const int DEBUG_NORMALS = 1;
            static const int DEBUG_BOXES = 2;
            static const int DEBUG_TRIANGLES = 4;
            static const int DEBUG_BOXES_AND_TRIANGLES = 8;

            /////////////////
            // STRUCTS
            /////////////////

            struct Ray {
                float3 origin;
                float3 direction;
                float3 inverseDirection;
            };

            struct RTMaterial {
                float4 color;
                float4 emissionColor;
                float4 specularColor;
                float emissionStrength;
                float smoothness;
                float specularProbability;
                float refractive_index;
                int flag;
                float4 checkerColor2;
                float checkerScale;
                float invCheckerScale;
            };

            struct Sphere {
                float3 origin;
                float radius;
                RTMaterial material;
            };

            struct HitInfo {
                bool did_hit;
                float3 hit_point;
                float dist;
                float3 normal;
                RTMaterial material;
                bool err;
                bool front_face;
            };

            struct Triangle
            {
                float3 Q;
                float3 u;
                float3 v;
                float3 n;
                float D;
                float3 w;
                float3 normal;
            };
            struct MeshInfo
            {
                int bvhNode;
                float4x4 worldToLocalMatrix;
                RTMaterial material;
            };
            struct MeshParent
            {
                int numMeshes;
                int meshStartIndex;
                float3 boundsMin;
                float3 boundsMax;
            };
            struct BVHNode
            {
                float3 boundsMin;
                float3 boundsMax;
                int index;
                int numTriangles;
            };

            /////////////////
            // VARIABLES
            /////////////////
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4x4 CamLocalToWorldMatrix;
            float3 ViewParams;
            StructuredBuffer<Sphere> Spheres;
            StructuredBuffer<Triangle> Triangles;
            StructuredBuffer<MeshInfo> Meshes;
            StructuredBuffer<BVHNode> BVHNodes;
            int Frame;
            int NumSpheres;
            int NumTriangles;
            int NumMeshes;
            int MaxBounces;
            int RaysPerPixel;
            int BoxTestCap;
            int TriangleTestCap;

            int DebugDisplayMode;

            /////////////////
            // RNG
            /////////////////
            uint NextRandom(inout uint state)
            {
                
                state = state * 747796405 + 2891336453;
                uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
                result = (result >> 22) ^ result;
                return result;
                
            } 

            float RandomValue(inout uint state)
            {
                return NextRandom(state) / 4294967295.0; // 2^32 - 1
            }
            float2 RandomPointInSquare(inout uint state) {
                float x = RandomValue(state);
                float y = RandomValue(state);
                return float2(x - 0.5, y-0.5);
            }
            float3 RandomPointInSphere(inout uint state)
            {
                float x;
                float y;
                float z;
                for (int i = 0; i < 100; i++) {

                    x = RandomValue(state) - 0.5;
                    y = RandomValue(state) - 0.5;
                    z = RandomValue(state) - 0.5;
                    if (x*x+y*y+z*z <= 0.25) {
                        break;
                    }
                }
                return normalize(float3(x,y,z));
            }

            /////////////////
            // HELPERS
            /////////////////
            float3 at(Ray r, float t) {
                return r.origin + r.direction*t;
            }
            float fifthPower(float x) {
                float x2 = x * x;
                return x2 * x2 * x;
            }


            /////////////////
            // HIT LOGIC
            /////////////////
            HitInfo hit_sphere(float3 sphereOrigin, float sphereRadius, Ray r) {
                HitInfo hit = (HitInfo)0;
                float3 oc = sphereOrigin - r.origin;
                float a = dot(r.direction, r.direction);
                float b = -2.0 * dot(r.direction, oc);
                float c = dot(oc, oc) - sphereRadius * sphereRadius;
                float discriminant = b*b - 4.0*a*c;
                if (discriminant < 0.0) 
                {
                    hit.did_hit = false;
                    } else {
                    hit.dist = (-b - sqrt(discriminant))/(2.0*a); 
                    if (hit.dist >= 0.0001) {

                        hit.did_hit = true;
                        hit.hit_point = at(r, hit.dist);
                        hit.normal = normalize(hit.hit_point - sphereOrigin);
                        if (dot(r.direction, hit.normal) > 0.0) {
                            hit.normal = -hit.normal;
                            hit.front_face = false;
                            } else {
                            hit.front_face = true;
                        }
                    }
                }
                return hit;
            }
            HitInfo hit_triangle(Triangle tri, Ray r) {
                HitInfo hit = (HitInfo)0;

                float denom = dot(tri.normal, r.direction);
                if (denom > -0.00001) {
                    return hit;
                }
                float t = (tri.D - dot(tri.normal, r.origin))/denom;
                if (t < 0.001)
                {
                    hit.did_hit = false;
                    } else {
                    float3 intersection = at(r, t);
                    float3 planar_hitpt_vector = intersection - tri.Q;
                    float alpha = dot(tri.w, cross(planar_hitpt_vector, tri.v));
                    float beta = dot(tri.w, cross(tri.u, planar_hitpt_vector));
                    if (alpha > 0.0 && beta > 0.0 && alpha + beta < 1.0) {
                        hit.did_hit = true;
                        hit.dist = t;
                        hit.hit_point = intersection;
                        hit.normal = tri.normal;
                        if (denom > 0.0) {
                            hit.normal = -hit.normal;
                            hit.front_face = false;
                            } else {
                            hit.front_face = true;
                        }
                    }
                }
                return hit;
            }
            float hit_aabb(float3 boundsMin, float3 boundsMax, Ray r) {
                // r.dir is unit direction vector of ray
                float3 tMin = (boundsMin - r.origin) * r.inverseDirection;
                float3 tMax = (boundsMax - r.origin) * r.inverseDirection;
                float3 t1 = min(tMin, tMax);
                float3 t2 = max(tMin, tMax);
                float tNear = max(max(t1.x, t1.y), t1.z);
                float tFar = min(min(t2.x, t2.y), t2.z);
                bool hit = tNear <= tFar && tFar > 0;
                return hit ? tNear : 1.#INF;
            }

            HitInfo hit_bvh_node(MeshInfo meshInfo, Ray r, inout int2 stats) {
                int stack[32];
                stack[0] = meshInfo.bvhNode;
                int stackIndex = 1;
                //int safetyLimit = 100;
                HitInfo closestHit = (HitInfo)0;
                closestHit.dist = 1.#INF;
                while (stackIndex > 0) {
                    int nodeIndex = stack[--stackIndex];
                    BVHNode node = BVHNodes[nodeIndex];
                    stats[0]++;
                    if (hit_aabb(node.boundsMin, node.boundsMax, r) < closestHit.dist) {
                        if (node.numTriangles > 0) {
                            HitInfo hit = (HitInfo)0;
                            for (int i = 0; i < node.numTriangles; i++) {
                                Triangle tri = Triangles[node.index + i];
                                stats[1]++;
                                hit = hit_triangle(tri, r);
                                if (hit.did_hit && hit.dist < closestHit.dist) {
                                    closestHit = hit;
                                    closestHit.material = meshInfo.material;
                                }
                            }
                            } else {
                            stats[0] += 2;
                            BVHNode childA = BVHNodes[node.index];
                            BVHNode childB = BVHNodes[node.index + 1];
                            float dstA = hit_aabb(childA.boundsMin, childA.boundsMax, r);
                            float dstB = hit_aabb(childB.boundsMin, childB.boundsMax, r);
                            if (dstA < closestHit.dist) {
                                if (dstB < closestHit.dist) {
                                    int closerIndex = dstA < dstB ? node.index : node.index + 1;
                                    int fartherIndex = dstA >= dstB ? node.index : node.index + 1;
                                    stack[stackIndex++] = fartherIndex;
                                    stack[stackIndex++] = closerIndex;
                                    
                                    } else {
                                    stack[stackIndex++] = node.index;
                                }
                                } else if (dstB < closestHit.dist) {
                                stack[stackIndex++] = node.index + 1;
                                
                            }
                        }
                    }
                }
                return closestHit;
            }

            /////////////////
            // TEXTURES
            /////////////////
            float4 get_material_color(HitInfo hit) {
                if (hit.material.flag == CheckerPattern) {
                    int x = (int)floor(hit.hit_point.x * hit.material.invCheckerScale);
                    int y = (int)floor(hit.hit_point.y * hit.material.invCheckerScale);
                    int z = (int)floor(hit.hit_point.z * hit.material.invCheckerScale);
                    if ((x + y + z) % 2 == 0) {
                        return hit.material.checkerColor2;
                    }
                }
                return hit.material.color;

            }

            /////////////////
            // DIELECTRICS
            /////////////////
            float reflectance(float cosine, float refraction_index) {
                // Use Schlick's approximation for reflectance.
                float r0 = (1 - refraction_index) / (1 + refraction_index);
                r0 = r0*r0;
                return r0 + (1-r0)*fifthPower(1 - cosine);
            }

            float3 refract(float3 ray_direction, float3 surface_normal, float refractive_index_ratio, inout uint rng) {
                float cos_theta = min(dot(-ray_direction, surface_normal), 1.0);
                float sin_theta = sqrt(1.0 - cos_theta*cos_theta);
                if (refractive_index_ratio * sin_theta > 1.0 || reflectance(cos_theta, refractive_index_ratio) > RandomValue(rng)) {
                    return reflect(ray_direction, surface_normal);
                }
                float3 perp = refractive_index_ratio * (ray_direction + cos_theta * surface_normal);
                float len = length(perp);
                float3 para = -sqrt(abs(1.0 - len*len)) * surface_normal;
                return perp + para;
            }



            /////////////////
            // RAY TRACING
            /////////////////
            float4 ray_color(Ray r, inout uint rng) {
                float previousRefractiveIndex = 1.0;
                HitInfo closestHit;
                float3 currentRayColor = float3(1,1,1);
                float3 light = float3(0,0,0);
                int2 stats;
                for(int bounce = 0; bounce < MaxBounces; bounce++) {

                    closestHit.dist = 1.#INF;
                    closestHit.did_hit = false;
                    for (int i = 0; i < NumSpheres; i++){
                        Sphere s = Spheres[i];
                        HitInfo hit = hit_sphere(s.origin, s.radius, r);
                        if (hit.did_hit) {
                            if (hit.dist < closestHit.dist) {
                                closestHit = hit;
                                closestHit.material = s.material;
                            }
                        }
                    }
                    // for (int i = 0; i < NumMeshParents; i++){
                        //     MeshParent meshParent = MeshParents[i];
                        //     HitInfo hit = hit_mesh_parent(meshParent, r);
                        //     if (hit.err) {
                            //         return float4(1,0,0,1);
                        //     }
                        //     if (hit.did_hit) {
                            //         if (hit.dist < closestHit.dist) {
                                //             closestHit = hit;
                            //         }
                        //     }
                    // }
                    for (int i0 = 0; i0 < NumMeshes; i0++) {
                        
                        HitInfo hit = hit_bvh_node(Meshes[i0], r, stats);
                        if (hit.did_hit && hit.dist < closestHit.dist)  {
                            closestHit = hit;
                        }
                        
                    }
                    if (!closestHit.did_hit) { 
                        // if (bounce == 0) {
                            light += currentRayColor * float3(0,0.14,0.74);
                            // } else {
                            // light += currentRayColor;
                        // }
                        break;
                    }
                    if (DebugDisplayMode == DEBUG_NORMALS) {
                        return float4(closestHit.normal, 1);
                    }
                    if (DebugDisplayMode != 0) {

                        float boxCount = (float)stats[0]/BoxTestCap;
                        float triCount = (float)stats[1]/TriangleTestCap;
                        if (DebugDisplayMode == DEBUG_BOXES) {
                            return boxCount > 1.0 ? float4(1,0,0,1) : boxCount;
                        }
                        if (DebugDisplayMode == DEBUG_TRIANGLES) {
                            return triCount > 1.0 ? float4(1,0,0,1) : triCount;
                        }
                        if (DebugDisplayMode == DEBUG_BOXES_AND_TRIANGLES) {
                            return triCount > 1.0 || boxCount > 1.0 ? 1 : float4(triCount, 0, boxCount, 1);
                        }
                    }
                    

                    bool isSpecular = RandomValue(rng) <= closestHit.material.specularProbability;
                    currentRayColor *= isSpecular ? closestHit.material.specularColor : get_material_color(closestHit);
                    light += currentRayColor * closestHit.material.emissionColor * closestHit.material.emissionStrength;
                    if (bounce == MaxBounces - 1) {
                        // if it's the last bounce, no need to calculate the next direction
                        break;
                    }
                    float3 specularDir = reflect(r.direction, closestHit.normal);
                    if (closestHit.material.flag == InvisibleLightSource)
                    {
                        // return float4(1,0,0,1);
                        r.origin = closestHit.hit_point + r.direction * 0.001;
                        continue;
                    }
                    float3 scatterDir;
                    if (closestHit.material.refractive_index >= 1.0) {
                        float rir = closestHit.front_face ? (previousRefractiveIndex/closestHit.material.refractive_index) : (closestHit.material.refractive_index/previousRefractiveIndex);
                        scatterDir = refract(r.direction, closestHit.normal, rir, rng);
                        previousRefractiveIndex = closestHit.material.refractive_index;
                        } else {
                        scatterDir = closestHit.normal + RandomPointInSphere(rng);
                        if(dot(closestHit.normal, scatterDir) < 0.0) {
                            scatterDir = -scatterDir;
                        }
                    }
                    r = (Ray)0;
                    r.origin = closestHit.hit_point;
                    r.direction = normalize(lerp(scatterDir, specularDir, isSpecular ? closestHit.material.smoothness : 0));
                    r.inverseDirection = 1.0f / r.direction;
                    
                }
                return float4(light, 1);
                


            }

            float4 displayFloat(float f) {
                if (f > 1.0) {
                    return float4(1,0,0,1);
                    } else if (f < 0.0) {
                    return float4(0,1,0,1);
                }
                return float4(f,f,f,1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                uint2 numPixels = _ScreenParams.xy;
                uint2 pixelCoord = i.uv * numPixels;
                uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
                uint rngState = pixelIndex + Frame * 719393;
                // return float4(RandomPointInSphere(rngState), 1);

                float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));
                float4 pixelcolor = float4(0,0,0,0);
                for (int j = 0; j < RaysPerPixel; j++) {

                    Ray r;
                    r.origin = _WorldSpaceCameraPos;
                    float2 offset = RandomPointInSquare(rngState);
                    r.direction = normalize((viewPoint - _WorldSpaceCameraPos)) + float3(offset.x/numPixels.x, offset.y/numPixels.y, 0);
                    r.inverseDirection = 1.0f/r.direction;
                    pixelcolor += ray_color(r, rngState)/float(RaysPerPixel);
                }

                return pixelcolor;
            }
            ENDCG
        }
    }
}
