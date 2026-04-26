# SkillForge Meilensteine

## M1: Erstes spielbares PvP-Spiel 🎮 ✅ FERTIG

**Ziel:** Zwei Spieler können sich matchen und 3 Runden Memory-Farben spielen

| Task | Beschreibung | Status |
|-------|------------------------------------------------------------------|---------|
| ✅ T1.1 | GameHub: vollständiger 3-Runden-Loop mit MatchOver | Gemerged |
| ✅ T1.2 | GameHub: MemoryColorsGame.GenerateData() pro Runde | Gemerged |
| ✅ T1.3 | GameHub: Score-Tracking beider Spieler, Gewinner-Ermittlung | Gemerged |
| ✅ T1.4 | Frontend: ShowColors-Phase automatisch triggern | Gemerged |
| ✅ T1.5 | Frontend: SignalR-URL konfigurierbar (env variable) | Gemerged |
| ✅ T1.6 | Program.cs: CORS für Frontend freigeben | Gemerged |
| ✅ T1.7 | Server-side Answer Validation | Gemerged |
| ✅ T1.8 | Backend: Bind to 0.0.0.0 für externe Verbindungen | Gemerged |

**Definition of Done:** Zwei Browser-Tabs können gegeneinander spielen, Sieger wird angezeigt. ✅

---

## M2: Auth + persistente Profile 🔐 ✅ FERTIG

**Ziel:** Spieler registrieren sich, loggen ein, XP wird gespeichert

| Task | Beschreibung | Status |
|------|---------------------------------------------|------|
| ✅ T2.1 | JWT-Token bei Login zurückgeben | Gemerged |
| ✅ T2.2 | Auth-Middleware für GameHub | Gemerged |
| ✅ T2.3 | XP nach Match in PostgreSQL persistieren | Gemerged |
| ✅ T2.4 | Password-Hashing: SHA256 → bcrypt | Gemerged |
| ✅ T2.5 | Frontend: Login/Register UI mit Token-Storage | Gemerged |
| ✅ T2.6 | Frontend: Skill-Anzeige nach Match (+24 XP) | Gemerged |

**Definition of Done:** User registriert, spielt 3 Matches, sieht XP-Aufstieg. ✅

---

## M3: Lobby + Skill-basiertes Matchmaking 🏆 ✅ FERTIG

**Ziel:** Echte Lobby mit Online-Spielerliste, skill-basiertes Matching

| Task | Beschreibung | Status |
|------|---------------------------------------|------|
| ✅ T3.1 | Lobby: Online-Spielerliste in Echtzeit | Gemerged |
| ✅ T3.2 | Matchmaking: Skill-basierte Queue (±2 Level, ältester zuerst) | Gemerged |
| ✅ T3.3 | Matchmaking: 30s Timeout → MatchmakingTimeout Event | Gemerged |
| ✅ T3.4 | Challenge: Spieler direkt herausfordern + Annehmen/Ablehnen | Gemerged |
| ✅ T3.5 | Disconnect-Handling: Walkover-Sieg für verbleibenden Spieler | Gemerged |
| ✅ T3.6 | Redis: Lobby, Matchmaking-Queue, Sessions + SignalR Backplane | Gemerged |

**Definition of Done:** 5+ Spieler gleichzeitig in Lobby, automatisches Matching funktioniert. ✅

---

## M4: Leaderboard + Skill-Tracking 📊

**Ziel:** Spieler sehen ihren Fortschritt und Ranking

| Task | Beschreibung | Status |
|------|--------------------------------------------------|------|
| ⏳ T4.1 | Leaderboard-API: Global + Land + Skill-Typ | Offen |
| ⏳ T4.2 | Percentile-Berechnung nach jedem Match | Offen |
| ⏳ T4.3 | Skill-History: Trendverlauf (Grafik) | Offen |
| ⏳ T4.4 | Frontend: Leaderboard mit Filtern | Offen |
| ⏳ T4.5 | Frontend: Spielerprofil mit Radar-Chart | Offen |
| ⏳ T4.6 | Insights: "Dein Memory hat sich um 15% verbessert" | Offen |
| ⏳ T4.7 | Materialized View für Leaderboard-Cache | Offen |

---

## M5: Zweites Spiel + Plugin-System ⚡

**Ziel:** Zweites Spiel (Speed) als Proof-of-Concept

| Task | Beschreibung | Status |
|------|---------------------------------------------|------|
| ⏳ T5.1 | SpeedGame Plugin implementieren (IGamePlugin) | Offen |
| ⏳ T5.2 | Game-Auswahl in Lobby | Offen |
| ⏳ T5.3 | Skill-Gewichtung pro Spiel (Memory vs Speed) | Offen |
| ⏳ T5.4 | Frontend: Spiel-spezifische UI | Offen |
| ⏳ T5.5 | Tests: KI-Agent testet beide Spiele | Offen |

---

**Aktueller Fokus:** M4 — Leaderboard & Skill-Tracking

_Updated: 2026-04-26_
