# DropMe privacy policy

*Last updated: November 2, 2026*

DropMe is a desktop app that runs entirely on your PC. It doesn't have accounts, ads, analytics
or telemetry, and it doesn't collect, sell or share personal data.

## What stays on your PC

- **Shelves and history.** Shelves only hold the paths of the files you drop on them. The list of
  recently closed shelves and your settings are saved in DropMe's folder in your user profile
  (`%APPDATA%\DropMe`, or the app's own storage for the Microsoft Store version).
- **Dropped content.** Images, text and links you drop from other apps are saved as files in a
  temporary folder (`%TEMP%\DropMe`) so they can be dragged elsewhere.
- **Error log.** If something goes wrong, technical details are written to `errors.log` in the same
  folder. It never leaves your PC.

Uninstalling DropMe removes these files.

## When DropMe uses the internet

- **Images dropped from web pages.** When you drop an image from a browser that only provides its
  web address, DropMe downloads that image from the site it came from, as your browser would.
- **Update check (version from GitHub only).** Once a day, DropMe asks GitHub's public API for the
  latest release of DropMe. The request contains no personal information beyond what any web request
  carries (such as your IP address, seen by GitHub). You can turn this off from the tray icon menu.
  The Microsoft Store version doesn't do this; the Store handles its updates.

These requests are subject to the privacy policies of the websites involved and of
[GitHub](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

## Contact

Questions about this policy: open an issue at <https://github.com/JohanRouyer/DropMe/issues>.
