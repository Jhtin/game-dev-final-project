using System.Collections.Generic;
using UnityEngine;

namespace HorrorEscape.Audio
{
    /// <summary>
    /// Centralized Audio Manager supporting ambient music, 2D/3D SFX,
    /// and built-in procedural sound generation so the game is never silent even without imported assets!
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource2D;
        [SerializeField] private AudioSource heartbeatSource;
        [SerializeField] private AudioSource fluorescentHumSource;

        [Header("Custom Sound Clips (Optional - Procedural fallbacks used if empty)")]
        public AudioClip ambientDroneClip;
        public AudioClip fluorescentHumClip;
        public AudioClip flashlightClickClip;
        public AudioClip footstepClip;
        public AudioClip doorOpenClip;
        public AudioClip doorCloseClip;
        public AudioClip doorUnlockClip;
        public AudioClip itemPickupClip;
        public AudioClip noteOpenClip;
        public AudioClip monsterSpottedClip;
        public AudioClip monsterGrowlClip;
        public AudioClip jumpscareClip;
        public AudioClip heartbeatClip;
        public AudioClip gunshotClip;
        public AudioClip dryFireClip;
        public AudioClip reloadClip;
        public AudioClip powerRestoreClip;
        public AudioClip distantFootstepsClip;
        public AudioClip distantGroanClip;
        public AudioClip typewriterClickClip;
        public AudioClip terminalBeepClip;

        private readonly Dictionary<string, AudioClip> proceduralCache = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSources();
            GenerateFallbackClipsIfNeeded();
            StartAmbientMusic();
        }

        private void EnsureAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.volume = 0.4f;
            }

            if (sfxSource2D == null)
            {
                sfxSource2D = gameObject.AddComponent<AudioSource>();
                sfxSource2D.loop = false;
                sfxSource2D.playOnAwake = false;
                sfxSource2D.volume = 0.8f;
            }

            if (heartbeatSource == null)
            {
                heartbeatSource = gameObject.AddComponent<AudioSource>();
                heartbeatSource.loop = true;
                heartbeatSource.playOnAwake = false;
                heartbeatSource.volume = 0f;
            }

            if (fluorescentHumSource == null)
            {
                fluorescentHumSource = gameObject.AddComponent<AudioSource>();
                fluorescentHumSource.loop = true;
                fluorescentHumSource.playOnAwake = false;
                fluorescentHumSource.volume = 0.35f;
            }
        }

        private void GenerateFallbackClipsIfNeeded()
        {
            if (ambientDroneClip == null) ambientDroneClip = CreateProceduralAmbientDrone();
            if (fluorescentHumClip == null) fluorescentHumClip = CreateProceduralFluorescentHum();
            if (flashlightClickClip == null) flashlightClickClip = CreateProceduralClick();
            if (footstepClip == null) footstepClip = CreateProceduralFootstep();
            if (doorOpenClip == null) doorOpenClip = CreateProceduralDoorCreak();
            if (doorCloseClip == null) doorCloseClip = CreateProceduralDoorThud();
            if (doorUnlockClip == null) doorUnlockClip = CreateProceduralUnlock();
            if (itemPickupClip == null) itemPickupClip = CreateProceduralPickup();
            if (noteOpenClip == null) noteOpenClip = CreateProceduralPaperRustle();
            if (monsterSpottedClip == null) monsterSpottedClip = CreateProceduralScareChord();
            if (jumpscareClip == null) jumpscareClip = CreateProceduralJumpscare();
            if (heartbeatClip == null) heartbeatClip = CreateProceduralHeartbeat();
            if (gunshotClip == null) gunshotClip = CreateProceduralGunshot();
            if (dryFireClip == null) dryFireClip = CreateProceduralDryFire();
            if (reloadClip == null) reloadClip = CreateProceduralReload();
            if (powerRestoreClip == null) powerRestoreClip = CreateProceduralPowerSurge();
            if (distantFootstepsClip == null) distantFootstepsClip = CreateProceduralDistantFootsteps();
            if (distantGroanClip == null) distantGroanClip = CreateProceduralDistantGroan();
            if (typewriterClickClip == null) typewriterClickClip = CreateProceduralTypewriterClick();
            if (terminalBeepClip == null) terminalBeepClip = CreateProceduralTerminalBeep();
        }

        public void StartAmbientMusic()
        {
            if (ambientDroneClip != null && musicSource != null)
            {
                musicSource.clip = ambientDroneClip;
                musicSource.Play();
            }

            if (fluorescentHumClip != null && fluorescentHumSource != null)
            {
                fluorescentHumSource.clip = fluorescentHumClip;
                fluorescentHumSource.Play();
            }
        }

        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || sfxSource2D == null) return;
            sfxSource2D.pitch = pitch;
            sfxSource2D.PlayOneShot(clip, volume);
        }

        public void PlayTypewriterClick(float volume = 0.35f)
        {
            if (typewriterClickClip != null && sfxSource2D != null)
            {
                sfxSource2D.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                sfxSource2D.PlayOneShot(typewriterClickClip, volume);
                sfxSource2D.pitch = 1.0f;
            }
        }

        public void PlayTerminalBeep(float volume = 0.4f)
        {
            if (terminalBeepClip != null && sfxSource2D != null)
            {
                sfxSource2D.pitch = 1.0f;
                sfxSource2D.PlayOneShot(terminalBeepClip, volume);
            }
        }

        public void PlayAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 20f)
        {
            if (clip == null) return;

            GameObject tempGO = new GameObject("TempAudio_" + clip.name);
            tempGO.transform.position = position;
            AudioSource source = tempGO.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f; // Full 3D sound
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();

            Destroy(tempGO, clip.length + 0.1f);
        }

        public void SetHeartbeatIntensity(float normalizedIntensity)
        {
            if (heartbeatSource == null || heartbeatClip == null) return;

            if (normalizedIntensity > 0.05f)
            {
                if (!heartbeatSource.isPlaying)
                {
                    heartbeatSource.clip = heartbeatClip;
                    heartbeatSource.Play();
                }
                heartbeatSource.volume = Mathf.Lerp(0.1f, 1.0f, normalizedIntensity);
                heartbeatSource.pitch = Mathf.Lerp(0.85f, 1.6f, normalizedIntensity);
            }
            else
            {
                if (heartbeatSource.isPlaying)
                {
                    heartbeatSource.Stop();
                }
            }
        }

        #region Procedural Audio Generators

        private AudioClip CreateProceduralAmbientDrone()
        {
            int sampleRate = 44100;
            int lengthSamples = sampleRate * 4; // 4 second loop
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Dark multi-frequency eerie sub-bass drone
                float tone1 = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f;
                float tone2 = Mathf.Sin(2f * Mathf.PI * 65.4f * t) * 0.25f; // dissonant minor second
                float wobble = Mathf.Sin(2f * Mathf.PI * 0.5f * t) * 0.15f;
                float noise = (Random.value * 2f - 1f) * 0.02f;
                data[i] = (tone1 + tone2 + wobble + noise) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Procedural_AmbientDrone", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralClick()
        {
            int sampleRate = 44100;
            int lengthSamples = sampleRate / 20; // 50ms
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / lengthSamples;
                float envelope = Mathf.Exp(-t * 20f);
                float wave = Mathf.Sin(2f * Mathf.PI * 1800f * ((float)i / sampleRate));
                data[i] = wave * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Procedural_FlashlightClick", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralFootstep()
        {
            int sampleRate = 44100;
            int lengthSamples = sampleRate / 10; // 100ms
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / lengthSamples;
                float env = Mathf.Exp(-t * 15f);
                float thud = Mathf.Sin(2f * Mathf.PI * 75f * ((float)i / sampleRate));
                float noise = (Random.value * 2f - 1f) * 0.2f;
                data[i] = (thud + noise) * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Footstep", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralDoorCreak()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.8f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * ((float)i / lengthSamples));
                float freq = 320f + Mathf.Sin(t * 30f) * 80f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = wave * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Procedural_DoorCreak", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralDoorThud()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.35f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / lengthSamples;
                float env = Mathf.Exp(-t * 10f);
                float wave = Mathf.Sin(2f * Mathf.PI * 85f * ((float)i / sampleRate));
                data[i] = wave * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Procedural_DoorThud", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralUnlock()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.25f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-((float)i / lengthSamples) * 12f);
                float metallic = Mathf.Sin(2f * Mathf.PI * 1200f * t) + Mathf.Sin(2f * Mathf.PI * 2400f * t);
                data[i] = metallic * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Unlock", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralPickup()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.4f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-((float)i / lengthSamples) * 6f);
                float note1 = Mathf.Sin(2f * Mathf.PI * 587.33f * t); // D5
                float note2 = Mathf.Sin(2f * Mathf.PI * 880.0f * t);   // A5
                data[i] = (note1 + note2) * env * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Pickup", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralPaperRustle()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.25f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float env = Mathf.Sin(Mathf.PI * ((float)i / lengthSamples));
                float noise = (Random.value * 2f - 1f) * 0.35f;
                data[i] = noise * env;
            }

            AudioClip clip = AudioClip.Create("Procedural_Paper", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralScareChord()
        {
            int sampleRate = 44100;
            int lengthSamples = sampleRate * 2; // 2 seconds
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-((float)i / lengthSamples) * 3f);
                // Sharp dissonant tritone chord
                float n1 = Mathf.Sin(2f * Mathf.PI * 440f * t);
                float n2 = Mathf.Sin(2f * Mathf.PI * 622.25f * t); // Tritone
                float n3 = Mathf.Sin(2f * Mathf.PI * 932.33f * t);
                data[i] = (n1 + n2 + n3) * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Procedural_ScareStinger", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralMonsterGrowl()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 1.5f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * ((float)i / lengthSamples));
                float lfo = Mathf.Sin(2f * Mathf.PI * 18f * t);
                float freq = 80f + lfo * 35f;
                float sound = Mathf.Sin(2f * Mathf.PI * freq * t) + (Random.value * 0.2f - 0.1f);
                data[i] = sound * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Procedural_MonsterGrowl", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralJumpscare()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 1.2f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float env = Mathf.Exp(-((float)i / lengthSamples) * 4f);
                float noise = (Random.value * 2f - 1f) * 0.7f;
                float screech = Mathf.Sin(2f * Mathf.PI * 1400f * ((float)i / sampleRate));
                data[i] = (noise + screech * 0.6f) * env * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Jumpscare", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralHeartbeat()
        {
            int sampleRate = 44100;
            int lengthSamples = sampleRate; // 1 second loop (Lub-dub)
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Beat 1 at 0.1s
                if (t >= 0.1f && t < 0.25f)
                {
                    float localT = (t - 0.1f) / 0.15f;
                    sample += Mathf.Sin(Mathf.PI * localT) * Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.8f;
                }
                // Beat 2 at 0.35s
                if (t >= 0.35f && t < 0.5f)
                {
                    float localT = (t - 0.35f) / 0.15f;
                    sample += Mathf.Sin(Mathf.PI * localT) * Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.65f;
                }

                data[i] = sample;
            }

            AudioClip clip = AudioClip.Create("Procedural_Heartbeat", lengthSamples, 1, sampleRate, true);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralFluorescentHum()
        {
            int sampleRate = 44100;
            int lengthSamples = sampleRate * 3; // 3 second loop
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                // 60Hz mains hum with 120Hz & 180Hz electrical ballast buzz + micro-crackle
                float hum60 = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.45f;
                float hum120 = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.30f;
                float hum180 = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.15f;
                float buzz = Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.08f;
                float hiss = (Random.value * 2f - 1f) * 0.02f;
                data[i] = (hum60 + hum120 + hum180 + buzz + hiss) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Procedural_FluorescentHum", lengthSamples, 1, sampleRate, true);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralGunshot()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.45f); // 450ms punchy report
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / lengthSamples;
                float env = Mathf.Exp(-t * 14f);
                float crack = (Random.value * 2f - 1f) * 0.75f;
                float bass = Mathf.Sin(2f * Mathf.PI * (140f - t * 80f) * ((float)i / sampleRate)) * 0.85f;
                data[i] = Mathf.Clamp((crack + bass) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_Gunshot", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralDryFire()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.05f); // 50ms click
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / lengthSamples;
                float env = Mathf.Exp(-t * 22f);
                float click = Mathf.Sin(2f * Mathf.PI * 2600f * ((float)i / sampleRate)) * 0.6f;
                data[i] = click * env;
            }

            AudioClip clip = AudioClip.Create("Procedural_DryFire", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralReload()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.65f); // 650ms slide/cylinder action
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float clack1 = (t > 0.1f && t < 0.2f) ? Mathf.Exp(-(t - 0.1f) * 40f) * Mathf.Sin(2f * Mathf.PI * 1800f * t) : 0f;
                float clack2 = (t > 0.4f && t < 0.5f) ? Mathf.Exp(-(t - 0.4f) * 45f) * Mathf.Sin(2f * Mathf.PI * 2200f * t) : 0f;
                data[i] = (clack1 + clack2) * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Reload", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralPowerSurge()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 1.8f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.Clamp01((float)i / lengthSamples) * Mathf.PI);
                float surgeFreq = 60f + t * 45f;
                float hum = Mathf.Sin(2f * Mathf.PI * surgeFreq * t) * 0.5f;
                float spark = (Random.value > 0.94f) ? (Random.value * 2f - 1f) * 0.4f : 0f;
                data[i] = (hum + spark) * env * 0.85f;
            }

            AudioClip clip = AudioClip.Create("Procedural_PowerSurge", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralDistantFootsteps()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 2.0f);
            float[] data = new float[lengthSamples];

            // 3 distant muffled footsteps
            float[] stepTimes = { 0.2f, 0.85f, 1.5f };
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float val = 0f;
                foreach (float st in stepTimes)
                {
                    if (t >= st && t < st + 0.15f)
                    {
                        float dt = t - st;
                        val += Mathf.Sin(2f * Mathf.PI * 65f * dt) * Mathf.Exp(-dt * 20f) * 0.45f;
                    }
                }
                data[i] = val;
            }

            AudioClip clip = AudioClip.Create("Procedural_DistantFootsteps", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralDistantGroan()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 3.0f);
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.Clamp01((float)i / lengthSamples) * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * (88f + Mathf.Sin(t * 1.5f) * 12f) * t) * 0.35f;
                float metal = Mathf.Sin(2f * Mathf.PI * 185f * t) * 0.12f;
                data[i] = (wave + metal) * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Procedural_DistantGroan", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralTypewriterClick()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.035f); // 35ms crisp keyclick
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 120f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.5f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.5f;
                data[i] = (noise + tone) * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("Procedural_TypewriterClick", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralTerminalBeep()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.08f); // 80ms gentle blip
            float[] data = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((float)i / lengthSamples * Mathf.PI);
                float tone = Mathf.Sin(2f * Mathf.PI * 780f * t) * 0.4f;
                data[i] = tone * env * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Procedural_TerminalBeep", lengthSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
