namespace JamKit.Skyline
{
    /// <summary>
    /// Floor 1's teaching beats (docs/SKYLINE-PLAN-FLOOR1.md, Task L1): the lobby lessons that M1 taught in the warm-up
    /// hall (moved here, same triggers), Nan's job sheet on the lift ride, Dot's calls on each tow, and CONCIERGE's lines
    /// for the greeting, the first wave, the alarm, the waves and the concession. One file, so the creator can rewrite every
    /// line in one place (they are mirrored in docs/SKYLINE-DRAFT-LINES.md). Every text is a draft for the creator except the
    /// ones the specs quote (<c>draft == false</c>). The spotlight is not on this floor, so it has no notice here.
    /// </summary>
    public static class FloorBeats
    {
        /// <summary>CONCIERGE announces a wave only on every third wave (3, 6, 9, ...): the notice repeats, but not for each
        /// one. The floor director asks <see cref="AnnouncesWave"/>.</summary>
        public const int WaveAnnounceEvery = 3;

        /// <summary>Whether the wave numbered <paramref name="waveNumber"/> (1-based) gets CONCIERGE's wave notice.</summary>
        public static bool AnnouncesWave(int waveNumber) => waveNumber >= WaveAnnounceEvery && waveNumber % WaveAnnounceEvery == 0;

        public static TeachingBeat[] Floor1() => new[]
        {
            // the staff hallway and the lobby
            new TeachingBeat { id = "nan-lobby", speaker = Speaker.Nan, trigger = BeatTrigger.LobbyArrive,
                text = "Unit's live. This is the Halcyon Grand. Warm up in the lobby, then the lift takes you down to the Galleria." },
            new TeachingBeat { id = "concierge-greeting", speaker = Speaker.Concierge, trigger = BeatTrigger.LobbyDesk,
                text = "Welcome to the Halcyon Grand. Guests are reminded that nothing in this lobby is for sale, and nothing in it is yours." },
            // the M1 lessons, kept: same words, same triggers, now at the bellhop, the counter, the stack
            new TeachingBeat { id = "dot-writ-spots", speaker = Speaker.Dot, trigger = BeatTrigger.PracticeBotSeen,
                text = "Practice bot! Tag its lens with the Writ: it cracks open a weak spot. Then hit the spot." },
            new TeachingBeat { id = "dot-writ-spot-made", speaker = Speaker.Dot, trigger = BeatTrigger.FirstSpotCreated,
                text = "That glowing crack? That's the spot. Shoot it!" },
            new TeachingBeat { id = "nan-double-jump-mantle", speaker = Speaker.Nan, trigger = BeatTrigger.NearPracticeLedge,
                text = "That counter's your practice. Tap jump twice for a second hop, then catch its lip and pull yourself up." },
            new TeachingBeat { id = "dot-breacher-boost", speaker = Speaker.Dot, trigger = BeatTrigger.NearBoostLedge,
                text = "Breacher kicks both ways. Jump, jump again, then blast the floor. Scrap's up top!" },
            new TeachingBeat { id = "dot-boost-done", speaker = Speaker.Dot, trigger = BeatTrigger.BoostLedgeReached,
                text = "Ha! Doesn't hurt you, either. Shoot again mid-air to climb higher." },
            // the lift ride: the job sheet fits inside the riser and the pre-drop
            new TeachingBeat { id = "nan-job-sheet", speaker = Speaker.Nan, trigger = BeatTrigger.LiftRiding,
                text = "Job sheet: three assets in the food court. Stand in a tow circle for ten seconds to take one. The clock's running, love." },
            // the floor
            new TeachingBeat { id = "concierge-first-wave", speaker = Speaker.Concierge, trigger = BeatTrigger.FloorStart,
                text = "Security has been informed of your arrival. Security would like a word. Security is on its way." },
            // the first tow ENDING (the creator's line and timing, 2026-10-14); dot-towed answers the second and later tows, not the first
            new TeachingBeat { id = "dot-clamped", speaker = Speaker.Dot, trigger = BeatTrigger.AssetTowed, draft = false,
                text = "Clamped! Get Moving!" },
            // the creator's line (2026-10-14): once, the first time the Unit wanders out of an asset's circle mid-tow
            new TeachingBeat { id = "dot-stay-near", speaker = Speaker.Dot, trigger = BeatTrigger.LeftCircleMidTow, draft = false,
                text = "Don't go too far, you need to stay nearby to transmat!" },
            new TeachingBeat { id = "concierge-alarm", speaker = Speaker.Concierge, trigger = BeatTrigger.AlarmRaised,
                text = "Attention: an item is leaving the premises without a receipt. Security has been invited to take an interest." },
            new TeachingBeat { id = "dot-towed", speaker = Speaker.Dot, trigger = BeatTrigger.AssetTowed, repeats = true,
                text = "Got it! Up and away. Next one, go go go." },
            new TeachingBeat { id = "concierge-wave", speaker = Speaker.Concierge, trigger = BeatTrigger.WaveAnnounce, repeats = true,
                text = "Reinforcements are arriving. Guests are advised that this is not an invitation to wait." },
            new TeachingBeat { id = "concierge-concede", speaker = Speaker.Concierge, trigger = BeatTrigger.AllTowed, draft = false,
                text = "Asset transfer... acknowledged. Under protest." },
            // the bot notices (the spotlight isn't on this floor)
            new TeachingBeat { id = "concierge-floor-care", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.FloorCareId, draft = false,
                text = "Please note: the floor-care unit operates at ankle height. Guests are advised to jump." },
            new TeachingBeat { id = "concierge-drone", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.DroneId,
                text = "Please note: security drones anticipate where guests are going. Guests are advised to change their minds." },
            new TeachingBeat { id = "concierge-gondola", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.GondolaId,
                text = "Please note: window cleaning is in progress outside the glass. Guests are advised that the squeegee leaves things behind." },
            // plating and special ammo
            new TeachingBeat { id = "dot-plating", speaker = Speaker.Dot, trigger = BeatTrigger.FirstPlatingBreak, draft = false,
                text = "Strip it! Scrap's armour!" },
            new TeachingBeat { id = "dot-special-ammo", speaker = Speaker.Dot, trigger = BeatTrigger.FirstSpecialAmmo,
                text = "Special rounds! Kill with the Writ and the wreck drops one. The Breacher runs dry; the Writ never does." },
        };
    }
}
