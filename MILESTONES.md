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

## M2: Auth + persistente Profile 🔐 ⏳ AKTIV

**Ziel:** Spieler registrieren sich, loggen ein, XP wird gespeichert

| Task | Beschreibung | Status |
|------|---------------------------------------------|------|
| ✅ T2.1 | JWT-Token bei Login zurückgeben | Gemerged (PR #16) |
| ✅ T2.2 | Auth-Middleware für GameHub | Gemerged (PR #19) |
| ⏳ T2.3 | XP nach Match in PostgreSQL persistieren | Offen |
| ✅ T2.4 | Password-Hashing: SHA256 → bcrypt | Gemerged (PR #15, #17) |
| ⏳ T2.5 | Frontend: Login/Register UI mit Token-Storage | Offen |
| ⏳ T2.6 | Frontend: Skill-Anzeige nach Match (+24 XP) | Offen |

**Definition of Done:** User registriert, spielt 3 Matches, sieht XP-Aufstieg.

---

## M3: Lobby + Skill-basiertes Matchmaking 🏆

**Ziel:** Echte Lobby mit Online-Spielerliste, skill-basiertes Matching

| Task | Beschreibung | Status |
|------|---------------------------------------|------|
| ⏳ T3.1 | Lobby: Online-Spielerliste in Echtzeit | Offen |
| ⏳ T3.2 | Matchmaking: Skill-basierte Queue | Offen |
| ⏳ T3.3 | Matchmaking: Timeout nach 30s → AI/Solo | Offen |
| ⏳ T3.4 | Challenge: Spieler direkt herausfordern | Offen |
| ⏳ T3.5 | Disconnect-Handling | Offen |
| ⏳ T3.6 | Redis: Matchmaking-Queue und Sessions | Offen |

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

**Aktueller Fokus:** M2 — Auth & persistente Profile (T2.3, T2.5, T2.6 offen)

_Updated: 2026-04-22_