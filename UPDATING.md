# GitHub updates

The Settings page now contains a **Check and install latest release** action. It uses `electron-updater`: when a newer stable version is found, it downloads the update, verifies the release metadata/checksum, and restarts into the installer.

Before it can contact GitHub, configure `owner` and `repo` in `src/shared/update-config.ts`. The GitHub repository must be public for this unauthenticated flow. Each published release must have a higher version than the installed app and include the electron-builder NSIS artifacts and `latest.yml` metadata. Do not publish an update from this checkout until the repository URL is confirmed.

No installer was generated as part of adding this feature. The current GitHub repository is still unknown, so the updater intentionally reports that configuration is missing until its owner and repository are supplied.
