using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    // Collectible placed by the city builder. Progress persists in PlayerPrefs.
    public sealed class Sunstone : MonoBehaviour
    {
        public static readonly List<Sunstone> All = new();
        public static event Action<Sunstone> Collected;
        public static int CollectedCount { get; private set; }
        public int id;
        public string place;
        public bool Taken { get; private set; }
        Transform gem, beam;
        Vector3 home;
        static Transform player;
        static HashSet<int> saved;
        const string Key = "sunstones";

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Start()
        {
            gem = transform.Find("Gem"); beam = transform.Find("Beam"); home = gem.localPosition;
            if (Saved.Contains(id)) Hide();
            Recount();
        }
        static HashSet<int> Saved
        {
            get
            {
                if (saved != null) return saved;
                saved = new HashSet<int>();
                foreach (var part in PlayerPrefs.GetString(Key, "").Split(','))
                    if (int.TryParse(part, out var value)) saved.Add(value);
                return saved;
            }
        }
        void Update()
        {
            if (Taken) return;
            float t = Time.time + id * 1.7f;
            gem.localPosition = home + Vector3.up * Mathf.Sin(t * 1.6f) * .12f;
            gem.localRotation = Quaternion.Euler(0, t * 70, 12 * Mathf.Sin(t));
            if (!player) { var motor = FindAnyObjectByType<WandererMotor>(); if (motor) player = motor.transform; else return; }
            if ((player.position + Vector3.up * .9f - gem.position).sqrMagnitude < 1.7f) Take();
        }
        void Take()
        {
            Hide();
            Saved.Add(id);
            PlayerPrefs.SetString(Key, string.Join(",", Saved)); PlayerPrefs.Save();
            Recount();
            Collected?.Invoke(this);
        }
        void Hide()
        {
            Taken = true;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }
        static void Recount()
        {
            int count = 0; foreach (var s in All) if (s.Taken) count++;
            CollectedCount = count;
        }
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(Key); saved = new HashSet<int>();
            foreach (var s in All) { s.Taken = false; foreach (var r in s.GetComponentsInChildren<Renderer>()) r.enabled = true; }
            Recount();
        }
    }
}
