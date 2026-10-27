using System;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Emits rhythmic condensation vapor breath puffs when the player is in freezing ambient air or low body warmth.
    /// Follows the VFX Lifecycle invariant by using a single pooled ParticleSystem attached to the player's head socket,
    /// with world-space simulation and zero runtime allocations.
    /// </summary>
    public class ColdBreathEmitter : MonoBehaviour
    {
        private Player? _player;
        private ParticleSystem? _particles;
        private ParticleSystemRenderer? _renderer;
        private Transform? _emitterTransform;

        private float _breathTimer = 0f;
        private float _nextBreathInterval = 3.8f;

        private void Awake()
        {
            _player = GetComponent<Player>();
            InitParticleSystem();
        }

        private void InitParticleSystem()
        {
            if (_player == null) return;

            Transform headAnchor = _player.m_eye != null ? _player.m_eye : transform;
            var go = new GameObject("VikingClimate_ColdBreath", typeof(ParticleSystem));
            go.transform.SetParent(headAnchor, false);
            go.transform.localPosition = new Vector3(0f, -0.05f, 0.18f); // Slightly below eye level, just in front of mouth
            go.transform.localRotation = Quaternion.identity;
            _emitterTransform = go.transform;

            _particles = go.GetComponent<ParticleSystem>();
            if (_particles == null) return;

            // 1. Main Module
            var main = _particles.main;
            main.duration = 1.0f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // Breath lingers in air as player moves forward
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.40f, 0.60f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
            main.startColor = new Color(0.92f, 0.96f, 1.0f, 0.35f);
            main.maxParticles = 32;

            // 2. Emission Module (bursted manually via Emit)
            var emission = _particles.emission;
            emission.enabled = false;

            // 3. Shape Module (forward mouth cone)
            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.02f;

            // 4. Size over Lifetime (vapor expands as it dissipates)
            var sizeOverLifetime = _particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 1.0f);
            sizeCurve.AddKey(1.0f, 2.4f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // 5. Color over Lifetime (soft fade-in then gentle dissipation)
            var colorOverLifetime = _particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new(new Color(0.95f, 0.98f, 1.0f), 0.0f),
                    new(new Color(0.90f, 0.95f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new(0.0f, 0.0f),
                    new(0.35f, 0.15f),
                    new(0.20f, 0.60f),
                    new(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            // 6. Velocity over Lifetime (slight warm thermal buoyancy drift upwards)
            var velocityOverLifetime = _particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.12f);

            // 7. Renderer setup
            _renderer = go.GetComponent<ParticleSystemRenderer>();
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (_renderer == null) return;
            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.shader != null
                && _renderer.sharedMaterial.shader.name != "Hidden/InternalErrorShader")
            {
                return;
            }

            if (ZNetScene.instance != null)
            {
                // 1. Try native vfx_Freezing prefab (official Valheim breath / freezing particle)
                var freezing = ZNetScene.instance.GetPrefab("vfx_Freezing");
                if (freezing != null)
                {
                    var psr = freezing.transform.Find("Breath")?.GetComponent<ParticleSystemRenderer>()
                        ?? freezing.transform.Find("soft cloud")?.GetComponent<ParticleSystemRenderer>()
                        ?? freezing.GetComponentInChildren<ParticleSystemRenderer>();
                    if (psr != null && psr.sharedMaterial != null)
                    {
                        _renderer.material = new Material(psr.sharedMaterial);
                        return;
                    }
                }

                // 2. Fallback to smoke or frost prefabs
                var fallbackPrefab = ZNetScene.instance.GetPrefab("smoke")
                    ?? ZNetScene.instance.GetPrefab("vfx_Frost")
                    ?? ZNetScene.instance.GetPrefab("vfx_ice_destroyed");
                if (fallbackPrefab != null)
                {
                    var psr = fallbackPrefab.GetComponentInChildren<ParticleSystemRenderer>();
                    if (psr != null && psr.sharedMaterial != null)
                    {
                        _renderer.material = new Material(psr.sharedMaterial);
                        return;
                    }
                }
            }
        }

        private void Update()
        {
            if (!ModConfig.ModEnabled.Value || _player == null || _particles == null) return;
            if (_player != Player.m_localPlayer) return;
            if (_player.IsDead() || _player.InBed() || _player.IsSwimming()) return;

            // Only emit breath puffs when freezing ambient (< 0°C) or body warmth drops into cold tiers (< 8°C)
            float bodyWarmth = SurvivalManager.CurrentState.BodyWarmth;
            float ambient = SurvivalManager.CurrentState.AmbientTemperature;
            bool isCold = bodyWarmth < 8.0f || ambient < 0.0f;

            if (!isCold)
            {
                _breathTimer = 0f;
                return;
            }

            _breathTimer += Time.deltaTime;
            if (_breathTimer >= _nextBreathInterval)
            {
                _breathTimer = 0f;

                // Exertion scales breathing rate: running/sprinting breathes faster (~1.8-2.2s vs ~3.8-4.4s resting/idle)
                bool isExerting = _player.IsRunning() || _player.InAttack();
                _nextBreathInterval = isExerting ? UnityEngine.Random.Range(1.8f, 2.2f) : UnityEngine.Random.Range(3.6f, 4.4f);

                EmitBreathPuff(bodyWarmth);
            }
        }

        private void EmitBreathPuff(float bodyWarmth)
        {
            if (_particles == null || _player == null) return;

            EnsureMaterial();

            // Align emitter forward with head direction
            Transform head = _player.m_eye != null ? _player.m_eye : transform;
            if (_emitterTransform != null)
            {
                _emitterTransform.position = head.position + (head.forward * 0.18f) - (head.up * 0.05f);
                _emitterTransform.rotation = Quaternion.LookRotation(head.forward);
            }

            // Severe cold produces slightly denser breath puffs
            int count = bodyWarmth < 0f ? UnityEngine.Random.Range(4, 6) : UnityEngine.Random.Range(3, 5);
            _particles.Emit(count);
        }

        private void OnDestroy()
        {
            if (_renderer != null && _renderer.material != null)
            {
                Destroy(_renderer.material);
            }
            if (_particles != null && _particles.gameObject != null)
            {
                Destroy(_particles.gameObject);
            }
        }
    }
}
