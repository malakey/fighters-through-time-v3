# Kit pass: lingering abilities — user decisions

Started 2026-10-31. The user's complaint: area-of-effect and damage-over-time Specials are "kind of useless" because they are easily avoided (the victim jumps away and waits it out), and the turret and Tesla coils "aren't very effective"; constructs can also be camped (drop one, fight beside it, always in range). This is the kit pass the ledger defers (`DEFER-KIT-PASS-MOMENTUM`).

Evidence (code read 2026-10-31, main `ce813449`): zone ticks carry no hitstun and no knockback in either mode; the Rift is 1.5 u in radius and the Vortex 1.8 u, against a Story single jump of about 3 u; the Vortex pull is 3 u/s against run speeds of 3.8–6.2 u/s; a coil arcs for 5 every 2 s (30 s, 4 u reach), the turret fires 4 bolts of 6 aimed once and then destroys itself, Venom is 2 HP a second; zone and construct hits never count as landed for D1a/D3 and earn no Rally echo or Momentum (they do earn Influence); each now costs a Momentum pip.

## Round 1

1. **Scope:** the lingering abilities only — every zone, construct, damage-over-time and slow (Tesla coil, Leonardo turret, Golden Ratio, Cleopatra's nest and Vortex, Einstein's Rift, Yorick's slow, Mozart's staff) and the status effects behind them. Instant Specials are untouched; Ultimates are left for later.
2. **Rule:** every lingering effect does at least one job — **hold** (keeps the target for a follow-up), **cash out** (a mark the hero's own direct hit detonates) or **deny** (a bigger area with a real penalty). Pure chip damage-over-time goes.
3. **Combo rewards:** only holds and cash-outs (a detonation, a synced construct strike, a hit that holds) count as landed and earn Momentum like a basic swing. Ordinary ticks do not.

## Round 2

4. **Constructs are hybrid:** a weak, slow attack of their own (spends no charges) so they still hold space, plus a strong synced strike with real hitstun when the hero lands a hit on a target in the construct's reach.
5. **Anti-camping: charges only.** Each construct holds a fixed number of synced strikes and fizzles when they are spent, so one pip buys a set amount of value. Not chosen: the crossfire dead zone, a break-it-for-a-reward rule, a telegraphed synced strike. (The user raised the concern that constructs are close to unavoidable and can be camped.)
6. **Modes:** the same mechanic in both modes, with numbers (radius, pull, charges) tuned per mode.

## Round 3

7. **Venom: a stronger damage-over-time that stops healing.** It keeps ticking, harder than 2 HP a second, and while poisoned the victim's Rally reclaim and healing are blocked (a deny job). Not chosen: Venom as a primer Cleopatra's hit cashes out; removing Venom.
8. **Slows: stronger and lingering.** Time Dilation becomes a heavier slow (about ×0.4 run speed and a lower jump) that stays 1.5–2 s after the victim leaves the zone (a hold job).
9. **Tesla coil: synced arc plus Lorentz discharge.** When Tesla lands a hit on a target within a coil's 4 u reach, the coil arcs it for a real hit with hitstun, spending a charge; his Lorentz Pulse near a coil discharges all its remaining charges at once as a burst. The fence between two coils stays as the coil's slow own attack.
10. **Leonardo turret: covering fire on his hits.** When Leonardo lands a hit on a target in the turret's range, it fires a fast bolt at that target with real hitstun, spending a charge. Its own slow shot stays. It no longer destroys itself after 4 bolts; it lasts until its charges or its lifetime run out.

## Round 4

11. **Venom in Story is just the stronger damage-over-time.** Mobs and bosses never heal, so the healing block bites only the Fighter opponent and the Level 13 Mirror clone. Not chosen: stopping a poisoned mob's poise recovery; extra damage from Cleopatra's hits.
12. **Serpent nest: weak own bite, strong synced lunge.** Its own bite becomes a short nip with no Venom. When Cleopatra lands a hit on a target near the nest, a serpent lunges for the 1 s hold plus Venom, spending a charge. Its reach grows from 60 px to about 2.5 u.
13. **Sandstorm Vortex: a pull you must jump out of.** A larger radius (about 2.5 u) and a pull stronger than walking speed, so only a jump, a roll or an air dodge escapes, and the stronger slow (decision 8) keeps the jump low. The final launch stays as its cash-out and counts as landed. Not chosen: an early collapse on a re-press.
14. **Relativity Rift: bigger, and his finisher collapses it too.** A larger radius (about 2.5 u) with the stronger slow. The collapse fires from an E=mc² burst or from Einstein's string finisher on a target inside, and counts as landed.

## Round 5

15. **Yorick gets the new slow.** Shakespeare's skull burst applies the stronger, lingering slow (decision 8) instead of ×0.7 for 2.5 s. No other change.
16. **Golden Ratio holds all three ticks.** Each of Leonardo's spiral ticks carries enough hitstun that a victim caught by the first takes all three.
17. **Mozart's staff is unchanged** (a traversal tool, not a lingering attack).
18. **Synced strikes trigger once per landed hit, per construct.** Each landed basic hit or direct Special hit on a target in a construct's reach triggers that construct once (a full string can spend up to three charges; two coils in reach both fire). Ticks and other constructs' strikes never trigger one, so there is no loop.
19. **Numbers are provisional, judged by hand.** The plan proposes values (for example 3 charges, 2.5 u radii), each recorded in the deviation ledger and measured by the bots; the user judges them in a hands-on session.
20. **Plan and build now.** (see below)

## Round 6 (after recon, `docs/recon-2026-10-31-kit/`)

21. **One slow factor for run and jump.** The heavier slow scales jump speed by the same ×0.4, so a slowed victim effectively cannot jump (about ×0.16 jump height). Not chosen: a separate, milder jump factor; a run-only slow.
22. **The Vortex is escaped by a roll or an air dodge.** Its pull is stronger than every run speed, but a roll or an air dodge ignores the pull while it travels. The Vortex applies its slow only when a victim leaves it (lingering), not while inside.
23. **Bosses take a milder, capped slow** (about ×0.7).
24. **Golden Ratio holds by pull, not hitstun.** The spiral draws a caught target toward its centre between ticks; only the first tick carries its normal hitstun. No poise or stun-lock rule changes.

## Round 7

25. **The Lorentz discharge replaces the chain.** When the Pulse fires, each of Tesla's coils within 8 u of him spends all its remaining charges as one burst on the nearest enemy in its reach; a Conductive-marked enemy can be hit from anywhere within 8 u of the coil. Today's 8-damage mark chain is gone.
26. **The turret's extras map to charges.** The glide bonus bolt stays and spends a charge; Clockwork Overdrive becomes +1 charge and a faster own shot, still against Extractors only in Story; Re-placement is unchanged.
27. **The Rift collapse deals damage** (about 8 to each target it pulls in), so it counts as landed by the ordinary rule.
28. **Venom blocks all healing except Defy** (Rally reclaim, healing orbs, Restoration Fonts, the checkpoint Mending heal), from any Venom source.

Decision 20 detail: **Plan and build now.** This reverses the earlier ruling that the hands-on session of the Momentum and Defy build comes first (`USER-MOMENTUM-HANDS-ON-BEFORE-KIT-PASS`): the session will judge the gauge, Defy and these kits together.
