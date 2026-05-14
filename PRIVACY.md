# RuckR Privacy Policy

**Last Updated:** 2026-05-10
**Effective Date:** 2026-05-10

## 1. Data We Collect

### Identity Information
- Username, email (if you choose to provide it)
- Identity records managed via ASP.NET Core Identity

### Game Data
- Player collections (creatures you've captured)
- Battle history (results, participants, timestamps)
- User profile (display name, biography, avatar)
- Progression data (level, experience, recruitment status)

### Location Data (GPS)
- **Your precise GPS coordinates are never stored long-term or exposed to other players.**
- GPS data is processed in real-time to check for nearby pitches and players.
- Location data is held in server memory for a maximum of **60 seconds** and is never persisted to the database.
- Discovery events (which pitch you discovered) are stored anonymously — no GPS coordinates are recorded.
- On disconnect or app closure, your location data is immediately discarded.

### Consent
- GPS data collection requires your explicit consent before it begins.
- You can withdraw consent at any time by disabling GPS on your device or through the app's privacy settings.
- Consent records are retained for 1 year after your last active session, then automatically purged.

## 2. How We Use Your Data

- **Identity data:** Authentication, authorization, and account recovery.
- **Game data:** Core gameplay — progression, battles, creature collection.
- **Location data (transient):** Real-time proximity checks for pitch discovery and nearby player visibility.
- **Analytics:** Aggregate, anonymous usage data sent via OpenTelemetry for performance monitoring (no PII).

## 3. Data Sharing

- **No personal data is sold or shared with third parties.**
- Anonymized aggregate gameplay statistics may be used for game balancing.
- Your username is visible only to players you interact with (challenges, battles).

## 4. Your Rights

### Right to Access
You can request a copy of all your data by contacting us.

### Right to Deletion (Right to Erasure)
You can permanently delete all your data via the **Settings** page:
- All game data (collections, battles, encounters, profiles) will be removed.
- Your identity account will be deleted.
- This action is **irreversible**.

### Right to Withdraw Consent
- Disable GPS permissions on your device at any time.
- Location processing stops immediately.

### Right to Data Portability
- Upon request, your game data can be exported in a machine-readable format.

## 5. Data Retention

| Data Type | Retention Period | Notes |
|-----------|-----------------|-------|
| Identity records | Until account deletion | Required for login |
| Game collections | Until account deletion | Core game state |
| Battle history | Until account deletion | Game progression |
| Location data | **60 seconds** (memory only) | Never persisted |
| Pitch discoveries | Permanent (anonymized) | No GPS coordinates stored |
| Consent records | 1 year after last session | Audit compliance |
| Rate limit records | 30 days | Abuse prevention |
| Player encounters | Until expiry (24h max) | Auto-purged by cleanup service |

## 6. Security

- All data in transit is encrypted (TLS 1.2+).
- Passwords are hashed with ASP.NET Core Identity's default PBKDF2 implementation.
- Connection strings and secrets are never hardcoded — managed via environment variables and `dotnet user-secrets`.
- Rate limiting prevents abuse of API endpoints.
- SQL injection and XSS are mitigated through Entity Framework parameterization and Blazor's automatic output encoding.

## 7. Children's Privacy

This game is not directed at children under 13. We do not knowingly collect data from children under 13.

## 8. Changes to This Policy

We may update this policy periodically. Check the "Last Updated" date at the top. Continued use of the service constitutes acceptance of any changes.

## 9. Contact

For privacy-related inquiries, data access requests, or deletion requests:
- In-game: Settings > Privacy
- Email: [to be added when available]