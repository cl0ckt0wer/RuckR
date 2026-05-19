# RuckR Design System

## Product Feel

RuckR is a mobile-first GPS rugby collector. The interface should feel like a live match-day map with a warm rugby pub accent: grounded, readable, and a little celebratory when something rare appears. The map remains the primary surface; overlays support action without turning the page into a card dashboard.

## Visual Vocabulary

- Base surface: dark green-black map overlays with high-contrast cream text.
- Accent: brass/amber for timed rare sightings and success moments.
- Support colors: turf green for ready/recruit states, slate for neutral status, red only for failure/expired states.
- Avoid: full brown/orange themes, purple gradients, decorative blobs, emoji, oversized hero-style panels, and generic glowing SaaS cards.

## Typography And Layout

- Use the existing app type until a broader type refresh is planned.
- Keep map panels compact and scannable: label, primary fact, supporting state, action.
- Mobile is the default. Bottom overlays must respect safe areas, keep 44px touch targets, and avoid covering GPS notices, map controls, or the recruit button.
- Text state must not rely on color alone. Timers, GPS quality, range, and recruit readiness need visible words.

## Spotlight Sighting Pattern

The Spotlight Sighting is a rugby pub sign on the map, not a separate event system.

Priority order:
1. Rare sighting identity: label, rarity, player name, timer.
2. Chase context: park, rough distance/range state, GPS quality.
3. Action context: success chance, recruit button, result feedback.

Use "recruit" as the player action verb everywhere in this flow.
