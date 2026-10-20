using UnityEngine;

namespace ColdBrewCase
{
    [RequireComponent(typeof(CircleCollider2D), typeof(AudioSource))]
    public sealed class FootstepCue : MonoBehaviour
    {
        [SerializeField] private bool playSound = true;
        private GameManager game;
        private NotesManager notes;
        private AudioSource sound;
        private AudioClip clip;
        private bool heard;
        private MeshRenderer[] markers;

        public bool Heard => heard;

        private void Awake()
        {
            game = Object.FindFirstObjectByType<GameManager>();
            if (game != null) notes = game.GetComponent<NotesManager>();
            markers = GetComponentsInChildren<MeshRenderer>();
            GetComponent<CircleCollider2D>().isTrigger = true;
            sound = GetComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0;
            sound.volume = .4f;
            const int sampleRate = 22050;
            float[] samples = new float[(int)(sampleRate * .42f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                float step = time < .22f ? time : time - .22f;
                float length = time < .22f ? .09f : .065f;
                if (step >= length) continue;
                samples[i] = Mathf.Sin(step * 110f * Mathf.PI * 2f) * Mathf.Exp(-step * 40f) * .7f;
            }
            clip = AudioClip.Create("Footsteps", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            sound.clip = clip;
        }

        private void OnTriggerEnter2D(Collider2D other) => Listen(other);
        private void OnTriggerStay2D(Collider2D other) => Listen(other);

        private void Listen(Collider2D other)
        {
            if (heard || game == null || notes == null || game.thief == null ||
                !game.CanMove || other.GetComponent<PlayerController>() == null) return;
            Vector2 target = game.thief.transform.position;
            Vector2 from = other.transform.position;
            if (!notes.Hear(from, target)) return;
            heard = true;
            sound.panStereo = Mathf.Clamp((notes.SoundPosition.x - from.x) / 5f, -.85f, .85f);
            if (playSound) sound.Play();
            foreach (MeshRenderer marker in markers) marker.enabled = false;
        }

        private void LateUpdate()
        {
            if (game == null || game.rooms == null) return;
            bool visible = !heard && game.rooms.IsVisible(game.rooms.RoomAt(transform.position));
            foreach (MeshRenderer marker in markers) marker.enabled = visible;
        }

        private void OnDestroy()
        {
            if (sound != null) sound.clip = null;
            if (clip != null) Destroy(clip);
        }
    }
}
