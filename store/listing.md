# Microsoft Store listing (English)

Copy these into Partner Center > DropMyShelf > Store listings > English. Character limits are noted.

## Product name

DropMyShelf

## Short description (Windows 10/11, up to 1,000 characters; shown at the top of the listing)

A drop zone that appears when you need it. Drag a file, give the mouse a quick shake, and a small
shelf pops up to hold it while you go find where it belongs.

## Description (up to 10,000 characters)

Moving files on Windows often means juggling two Explorer windows, or dragging something halfway
across the screen only to realize the destination isn't open yet. DropMyShelf gives you a place to put
things down for a moment.

1. Start dragging a file, a folder, an image from a web page, some text or a link.
2. Shake the mouse left and right. A shelf appears right next to the cursor.
3. Drop it on the shelf, then take your time: open the right folder, switch apps, collect more files.
4. Drag the shelf to where everything should go.

No window to keep open, no hotkey to remember. DropMyShelf stays in the notification area until you shake.

Shelves only keep references to your files: nothing is copied or deleted until you drag it
somewhere yourself. Recently closed shelves stay one click away from the tray icon.

DropMyShelf runs entirely on your PC, with no account, ads or tracking.

Known limitation: the shake is detected whenever the left mouse button is held, so it can
occasionally trigger while selecting text or moving a window. Closing the shelf is one click, and an
empty shelf closes by itself when you let go.

## Product features (up to 20, 200 characters each)

- Shake the mouse while dragging to open a shelf next to the cursor
- Collect files from several places, then move them all in a single drag
- Click a shelf to see every file and drag them out one by one
- Images dragged from a browser are saved as real image files
- Text and links become .txt and .url files you can drop anywhere
- Reopen recently closed shelves from the tray icon (keep 3 to 50)
- Never steals focus from the app you're dragging from
- Optional start with Windows
- No account, no ads, no tracking

## Search terms (up to 7, 30 characters each)

drag and drop, file shelf, drop zone, file manager, productivity, clipboard, shelf

## Category

Productivity

## Privacy policy URL

https://github.com/JohanRouyer/DropMyShelf/blob/main/PRIVACY.md

## Website

https://github.com/JohanRouyer/DropMyShelf

## Support contact

https://github.com/JohanRouyer/DropMyShelf/issues

## Copyright and trademark info

Copyright (c) 2026 JohanRouyer

## Screenshots (1920x1080)

store/screenshots/1-shelves.png, 2-library.png, 3-drag-shake-drop.png
(regenerate with `python tools\make_store_screenshots.py`)

## Trailer

store/trailer/DropMyShelf-trailer.mp4 (1920x1080, H.264, 27 s, no audio) with its thumbnail
store/trailer/DropMyShelf-trailer-thumbnail.png (regenerate with `python tools\make_trailer.py`)

## Notes for certification (Submission options > Notes for certification)

DropMyShelf is a WPF desktop app. It uses runFullTrust because it installs a low-level mouse hook
(SetWindowsHookEx, WH_MOUSE_LL) to detect a left-right shake of the mouse while the left button is
held during a drag, and then shows a small topmost window that accepts dropped files. To test: drag
any file from File Explorer and shake the mouse left and right; drop the file on the shelf that
appears; drag the shelf onto a folder. Left-click the tray icon to open the DropMyShelf window.
The app starts with Windows through a package startup task the user can turn off in
Settings > Apps > Startup. The Store version does not update itself.
