# Unity Main Camera Streaming

This project now exposes the Unity main camera feed through the existing Node.js companion server so that the scene can be viewed from a web browser.

## Prerequisites

- Unity project uses the `Assets/Scripts/MainCameraStreamer.cs` behaviour.
- Node.js 18+ installed to run the `server` folder.

## Unity Setup

1. Add the **MainCameraStreamer** component (found in `Assets/Scripts/MainCameraStreamer.cs`) to an object in your scene, for example an empty `GameObject` named `Streaming`.
2. Optional: Drag the camera you want to stream into the **Source Camera** field. If you leave it blank the script automatically uses `Camera.main`.
3. Adjust the capture settings if needed:
   - `Capture Width / Height`: output resolution in pixels.
   - `Jpeg Quality`: trade-off between file size and clarity.
   - `Upload Interval Seconds`: how frequently frames are uploaded (lower values = smoother video, higher bandwidth).
   - `Upload Url`: defaults to `http://localhost:3000/frame`. Change this if the server runs on another machine or port.
4. Enter Play mode to start sending frames. The component uploads after `WaitForEndOfFrame`, so avoid disabling the game object while streaming.

## Server Setup

1. Open a terminal in `server/` and install dependencies once:
   ```bash
   npm install
   ```
2. Start the server:
   ```bash
   npm run dev
   ```
   or `npm start` for the production build.
3. The server listens on `http://localhost:3000` by default. Adjust the `PORT` environment variable if you need a different port.

## Viewing the Stream

- Visit `http://localhost:3000/` in a browser. The page now includes an `<img>` element that refreshes whenever a new frame arrives.
- When the Unity application is streaming, the image becomes visible. If no frame has been uploaded yet the viewer shows nothing.

## Notes & Troubleshooting

- The stream uses JPEG over HTTP, triggered by WebSocket notifications. Expect a bursty, still-image style feed rather than low-latency video.
- Larger resolutions or shorter upload intervals increase bandwidth and server CPU usage. Tune the three capture settings to balance quality and performance.
- If the webpage never shows a frame:
  - Confirm the Unity console does not log `[MainCameraStreamer] Upload failed`.
  - Check that `Upload Url` points to the reachable server (e.g., replace `localhost` with the machine's LAN IP when testing from another device).
  - Make sure firewalls allow the HTTP POST request to `/frame`.
