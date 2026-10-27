#!/usr/bin/env python3
"""
Makes a demo copy of the bot's database, full of made-up game nights, for showing the dashboard off.

Copies src/DungeonWorldBot/SanaSolem.db to SanaSolem.demo.db next to it (the real one is only read), then adds ten
weekly sessions, each with a fight or two played out blow by blow and an evening of rolls in the game chat. The party,
NPCs and real rolls are kept as they are. Run it again for a fresh copy; the same seed makes the same nights.

    python3 scripts/seed-demo.py

Then run the dashboard against it with the "dashboard-demo" launch config, or:

    dotnet run --project src/DungeonWorldBot.Dashboard -- --DatabasePath=../DungeonWorldBot/SanaSolem.demo.db

(The path is from the Dashboard's folder, which dotnet run works from.)
"""

import random
import sqlite3
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "src" / "DungeonWorldBot" / "SanaSolem.db"
DEMO = SOURCE.with_name("SanaSolem.demo.db")

GAME_CHAT = 1325143775139467325
DUNGEON_MASTER = 473305025301577728
DISCORD_EPOCH_MS = 1420070400000

STR, DEX, CON, INT, WIS, CHA = range(6)

# Who's in the party, the stats they lean on, what they roll in a fight and out of one, and how hard they hit
PARTY = {
    "Zephyr": dict(
        mods={STR: -1, DEX: 2, CON: 0, INT: 0, WIS: 1, CHA: -1},
        attacks=[("Volley", DEX), ("Called Shot", DEX), ("Volley", DEX)],
        other=[("Defy Danger", DEX), ("Discern Realities", WIS), ("Hunt and Track", WIS)],
        damage=8),
    "Mr. Hatter": dict(
        mods={STR: 2, DEX: 0, CON: 1, INT: 0, WIS: -1, CHA: 0},
        attacks=[("Hack and Slash", STR), ("Hack and Slash", STR), ("Defend", CON)],
        other=[("Read the Room", INT), ("Parley", CHA), ("Defy Danger", CON)],
        damage=10),
    "Ziedrich Floran": dict(
        mods={STR: -1, DEX: 0, CON: 0, INT: 2, WIS: 1, CHA: 0},
        attacks=[("Cast a Spell", INT), ("Cast a Spell", INT), ("Defy Danger", DEX)],
        other=[("Spout Lore", INT), ("Discern Realities", WIS), ("Parley", CHA)],
        damage=6),
    "Edena Trifur": dict(
        mods={STR: 0, DEX: 1, CON: 0, INT: -1, WIS: 2, CHA: 0},
        attacks=[("Shapeshift", WIS), ("Hack and Slash", STR), ("Cast a Spell", WIS)],
        other=[("Spout Lore", INT), ("Discern Realities", WIS), ("Defy Danger", DEX)],
        damage=6),
}

# What the party runs into: name, HP, armor, damage die, the attack it makes
MONSTERS = {
    "Goblin": (3, 1, 6, "Stab"),
    "Goblin Orkaster": (5, 0, 8, "Firebolt"),
    "Bandit": (6, 1, 6, "Rusty Blade"),
    "Bandit Captain": (12, 2, 10, "Cleave"),
    "Dire Wolf": (8, 1, 8, "Savage Bite"),
    "Skeleton": (7, 1, 6, "Bone Club"),
    "Ghoul": (10, 1, 8, "Paralysing Claws"),
    "Owlbear": (16, 2, 10, "Crushing Hug"),
    "Cultist": (5, 0, 6, "Ritual Dagger"),
    "Cult Leader Varrow": (14, 1, 10, "Shadow Lash"),
    "Bog Hag": (14, 1, 10, "Drowning Curse"),
    "Swamp Troll": (20, 3, 10, "Club Swing"),
    "Wight": (12, 2, 8, "Life Drain"),
    "Young Black Dragon": (24, 4, 12, "Acid Breath"),
}

NPCS = {
    "Elden Notetaker": [("Magic Missile", "💥 {d} damage"), ("Counterspell", None), ("Arcane Shield", None)],
    "Serena": [("Shortbow", "💥 {d} damage"), ("Bandage", None)],
    "Jin": [("Knife Throw", "💥 {d} damage"), ("Distract", None)],
}

# Each night: its title and the fights on it, with who turned up to each
NIGHTS = [
    ("The Road to Mirefen", [("Ambush at Thornwood", "Thornwood Road", ["Goblin", "Goblin", "Goblin", "Goblin Orkaster"])]),
    ("Wolves at the Gate", [("Siege of the Waystation", "Greyhollow Waystation", ["Dire Wolf", "Dire Wolf", "Dire Wolf"])]),
    ("A Toll Unpaid", [("Bridge Toll", "Mirefen Bridge", ["Bandit", "Bandit", "Bandit"]),
                       ("The Captain's Last Stand", "Bandit Camp", ["Bandit Captain", "Bandit", "Bandit"])]),
    ("Bones in the Barrow", [("The Barrow Door", "Old King's Barrow", ["Skeleton", "Skeleton", "Skeleton", "Skeleton"])]),
    ("Down Among the Dead", [("Ghouls in the Ossuary", "The Ossuary", ["Ghoul", "Ghoul"]),
                             ("The Wight-Lord Wakes", "Barrow Throne Room", ["Wight", "Skeleton", "Skeleton"])]),
    ("Feathers and Fur", [("Owlbear!", "Whisperpine Glade", ["Owlbear"])]),
    ("The Hooded Choir", [("Raid on the Chapel", "Ruined Chapel", ["Cultist", "Cultist", "Cultist", "Cultist"])]),
    ("Varrow's Rite", [("Stop the Ritual", "Under-Chapel", ["Cult Leader Varrow", "Cultist", "Cultist"])]),
    ("Into the Mire", [("Troll Under the Causeway", "Sunken Causeway", ["Swamp Troll"]),
                       ("The Hag's Bargain", "Hag's Hut", ["Bog Hag", "Dire Wolf"])]),
    ("Black Wings over Mirefen", [("The Dragon's Lair", "Drowned Tower", ["Young Black Dragon", "Cultist", "Cultist"])]),
]

rng = random.Random(1325)


def stamp(at: datetime) -> str:
    return at.astimezone(timezone.utc).strftime("%Y-%m-%d %H:%M:%S.%f+00:00")


used_ids = set()


def snowflake(at: datetime) -> int:
    """A Discord message ID made at that time, which is where the dashboard reads a roll's time from."""
    while True:
        value = ((int(at.timestamp() * 1000) - DISCORD_EPOCH_MS) << 22) | rng.getrandbits(22)
        if value not in used_ids:
            used_ids.add(value)
            return value


def outcome(total: int) -> str:
    return "Strong Hit (10+)" if total >= 10 else "Weak Hit (7-9)" if total >= 7 else "Miss (6-)"


def main():
    DEMO.unlink(missing_ok=True)
    with sqlite3.connect(SOURCE) as real, sqlite3.connect(DEMO) as demo:
        real.backup(demo)
    db = sqlite3.connect(DEMO)

    party = {name: cid for cid, name in db.execute("SELECT ID, Name FROM Characters")}
    npcs = {name: nid for nid, name in db.execute("SELECT ID, Name FROM Npcs")}
    missing = set(PARTY) - set(party)
    if missing:
        raise SystemExit(f"Not in the database: {', '.join(sorted(missing))}")

    # A clean slate for what the demo makes up: no test fights or sessions
    for table in ["MoveRequests", "EncounterActions", "EncounterParticipants", "Enemies", "Encounters", "Sessions"]:
        db.execute(f"DELETE FROM {table}")
    for name, (hp, armor, die, _) in MONSTERS.items():
        db.execute("INSERT OR IGNORE INTO MonsterTemplates (Name, MaxHP, Armor, Damage) VALUES (?, ?, ?, ?)",
                   (name, hp, armor, f"d{die}"))

    def roll(user: int, at: datetime, move=None, stat=None, mod=0) -> tuple[int, int]:
        first, second = rng.randint(1, 6), rng.randint(1, 6)
        total = first + second + mod
        message = snowflake(at)
        db.execute(
            "INSERT INTO Rolls (MessageID, ChannelID, UserID, First, Second, Total, Move, Stat, Modifier) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (message, GAME_CHAT, user, first, second, total, move, stat, mod))
        return message, total

    # Fridays at 7pm Pacific, weekly, ending the week before last
    first_night = datetime(2026, 8, 8, 2, 0, tzinfo=timezone.utc)
    fights_made = rolls_before = 0
    rolls_before = db.execute("SELECT COUNT(*) FROM Rolls").fetchone()[0]

    for week, (title, fights) in enumerate(NIGHTS):
        start = first_night + timedelta(weeks=week, minutes=rng.randint(-10, 20))
        length = timedelta(hours=rng.uniform(3, 4.5))
        end = start + length
        db.execute("INSERT INTO Sessions (Title, StartedAt, EndedAt) VALUES (?, ?, ?)", (title, stamp(start), stamp(end)))

        # Fights spread through the night, with exploring and talking around them
        fight_starts = sorted(start + length * rng.uniform(0.2 + i * 0.4, 0.4 + i * 0.4) for i in range(len(fights)))
        busy = []

        for (name, scene, roster), at in zip(fights, fight_starts):
            fights_made += 1
            cur = db.execute(
                "INSERT INTO Encounters (Name, StartedAt, AnnouncedAt, SceneName) VALUES (?, ?, ?, ?)",
                (name, stamp(at), stamp(at + timedelta(seconds=40)), scene))
            encounter = cur.lastrowid

            counts = {m: roster.count(m) for m in roster}
            seen = {}
            enemies = []
            for monster in roster:
                hp, armor, die, attack = MONSTERS[monster]
                seen[monster] = seen.get(monster, 0) + 1
                label = f"{monster} {seen[monster]}" if counts[monster] > 1 else monster
                cur = db.execute(
                    "INSERT INTO Enemies (Name, EncounterID, CurrentHP, MaxHP, Armor, Damage) VALUES (?, ?, ?, ?, ?, ?)",
                    (label, encounter, hp, hp, armor, f"d{die}"))
                enemies.append(dict(id=cur.lastrowid, name=label, hp=hp, max=hp, armor=armor, die=die, attack=attack))

            for who in PARTY:
                db.execute("INSERT INTO EncounterParticipants (EncounterID, CharacterID) VALUES (?, ?)", (encounter, party[who]))
            helper = rng.choice([n for n in NPCS if n in npcs]) if npcs and rng.random() < 0.6 else None
            if helper:
                db.execute("INSERT INTO EncounterParticipants (EncounterID, NpcID) VALUES (?, ?)", (encounter, npcs[helper]))

            def log(when, action, actor, roll_text=None, target=None, result=None, character=None, enemy=None, npc=None, message=None):
                db.execute(
                    "INSERT INTO EncounterActions (Action, Actor, At, CharacterID, EncounterID, EnemyID, MessageID, NpcID, Result, Roll, Target) "
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    (action, actor, stamp(when), character, encounter, enemy, message, npc, result, roll_text, target))

            def hurt(enemy, damage):
                # Always at least a scratch, so the replays don't fill up with hits that did nothing
                taken = max(1, damage - enemy["armor"])
                enemy["hp"] = max(0, enemy["hp"] - taken)
                return taken

            clock = at + timedelta(minutes=2)
            for _ in range(12):
                standing = [e for e in enemies if e["hp"] > 0]
                if not standing:
                    break
                for who in rng.sample(list(PARTY), len(PARTY)):
                    standing = [e for e in enemies if e["hp"] > 0]
                    if not standing:
                        break
                    sheet = PARTY[who]
                    move, stat = rng.choice(sheet["attacks"])
                    mod = sheet["mods"][stat]
                    clock += timedelta(seconds=rng.randint(40, 150))
                    message, total = roll(party[who], clock, move, stat, mod)
                    target = rng.choice(standing)
                    roll_text = f"{total} · {outcome(total)}"
                    if move == "Defend":
                        log(clock, move, who, roll_text, character=party[who], message=message,
                            result="🛡️ holds the line" if total >= 7 else None)
                    elif total >= 7:
                        damage = rng.randint(1, sheet["damage"]) + (2 if total >= 10 and move == "Called Shot" else 0)
                        taken = hurt(target, damage)
                        log(clock, move, who, roll_text, target["name"], f"💥 {taken} damage", character=party[who], message=message)
                    else:
                        log(clock, move, who, roll_text, target["name"], character=party[who], message=message)

                    # A miss (and now and then a weak hit) is the GM's chance to hit back
                    if total < 7 or (total < 10 and rng.random() < 0.4):
                        standing = [e for e in enemies if e["hp"] > 0]
                        if standing:
                            enemy = rng.choice(standing)
                            hit = rng.randint(1, enemy["die"])
                            clock += timedelta(seconds=rng.randint(30, 90))
                            log(clock, enemy["attack"], enemy["name"], f"d{enemy['die']} = {hit}", who, f"💥 {hit} damage", enemy=enemy["id"])

                if helper and rng.random() < 0.5:
                    standing = [e for e in enemies if e["hp"] > 0]
                    if standing:
                        action, result = rng.choice(NPCS[helper])
                        total = rng.randint(2, 12) + 1
                        target = rng.choice(standing)
                        clock += timedelta(seconds=rng.randint(30, 90))
                        if result and total >= 7:
                            result = result.format(d=hurt(target, rng.randint(1, 6)))
                        elif result:
                            result = None
                        log(clock, action, helper, f"{total} · {outcome(total)}", target["name"] if result or total < 7 else None,
                            result, npc=npcs[helper])

            # Whatever's left standing runs off, and the fight's over
            for enemy in enemies:
                db.execute("UPDATE Enemies SET CurrentHP = ? WHERE ID = ?", (enemy["hp"], enemy["id"]))
            ended = clock + timedelta(minutes=rng.randint(1, 4))
            db.execute("UPDATE Encounters SET EndedAt = ?, EndAnnouncedAt = ? WHERE ID = ?", (stamp(ended), stamp(ended), encounter))
            busy.append((at, ended))

        # The rest of the night: exploring, talking, looking around, and the GM rolling behind the screen
        for _ in range(rng.randint(30, 55)):
            when = start + length * rng.random()
            if any(a <= when <= b for a, b in busy):
                continue
            if rng.random() < 0.15:
                roll(DUNGEON_MASTER, when)
                continue
            who = rng.choice(list(PARTY))
            sheet = PARTY[who]
            if rng.random() < 0.25:
                roll(party[who], when)
            else:
                move, stat = rng.choice(sheet["other"])
                roll(party[who], when, move, stat, sheet["mods"][stat])

    db.commit()
    added = db.execute("SELECT COUNT(*) FROM Rolls").fetchone()[0] - rolls_before
    actions = db.execute("SELECT COUNT(*) FROM EncounterActions").fetchone()[0]
    db.close()
    print(f"Made {DEMO.relative_to(ROOT)}: {len(NIGHTS)} sessions, {fights_made} fights, {actions} fight actions, {added} new rolls")


if __name__ == "__main__":
    main()
