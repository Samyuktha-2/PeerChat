# PeerChat

PeerChat is a Windows WPF peer-to-peer chat application built with C# and TCP sockets. It allows two users on the same network to connect directly, exchange text messages, share images, send videos, view typing status, switch themes, and inspect debug logs for transmitted frames.

The project is designed as a simple learning-friendly peer-to-peer messenger. It does not depend on a central server after connection setup: one user hosts a TCP listener, and the other user connects directly to that host.

## Features

- Peer-to-peer TCP connection
- Host and client connection modes
- Text messaging
- Image sending and preview
- Chunked video transfer
- Inline video transfer progress
- Video thumbnail generation from the transferred file
- Play received or sent videos using the system default video player
- Typing status
- Peer disconnect handling
- Light and dark themes
- Debug log panel for sent and received frames

## High-Level Architecture

PeerChat follows an MVVM-style WPF architecture.

```text
MainWindow
   |
   v
MainVM
   |
   +--> ConnectionWindowVM
   |       |
   |       +--> NetworkService
   |               |
   |               +--> TcpListener / TcpClient
   |
   +--> ChatWindowVM
           |
           +--> MessageProtocol
           |       |
           |       +--> SendFrameAsync / ReceiveFrameAsync
           |
           +--> ObservableCollection<MessageModel>
           +--> ObservableCollection<DebugModel>
```

## Project Structure

```text
PeerChat/
  Command/        RelayCommand helpers for MVVM commands
  Helpers/        WPF value converters
  Model/          Message, user, and debug log models
  Services/       TCP networking and message framing
  Theme/          Light and dark theme resource dictionaries
  View/           WPF user controls
  ViewModel/      Main app, connection, and chat view models
```

## Core Design

### Connection Flow

One user selects Host mode and starts listening on a port. Another user selects Client mode and connects to the host IP address and port.

```text
Host user
   |
   v
Start TcpListener
   |
   v
Wait for peer connection
   |
   v
Open ChatWindow

Client user
   |
   v
Connect to host IP and port
   |
   v
Open ChatWindow
```

### Message Protocol

PeerChat uses a custom frame-based protocol on top of TCP.

Each frame has:

```text
[1 byte message type]
[4 bytes payload length]
[payload bytes]
```

Frame types used by the app:

```text
0x01 - Text
0x02 - Image
0x03 - Video
0x04 - Typing status
0x05 - Disconnect
0x06 - Display name handshake
```

The protocol is implemented in:

```text
Services/MessageProtocol.cs
```

## Video Transfer Design

Videos are transferred in chunks instead of loading the full video into memory. This makes video transfer safer for larger files.

Sender flow:

```text
Select video
   |
   v
Open FileStream
   |
   v
Read 64 KB chunk
   |
   v
Send chunk as frame type 0x03
   |
   v
Update progress and debug log
   |
   v
Repeat until complete
```

Receiver flow:

```text
Receive 0x03 frame
   |
   v
Create temp file on first chunk
   |
   v
Write chunk bytes directly to FileStream
   |
   v
Update progress and debug log
   |
   v
When complete, close file
   |
   v
Generate thumbnail
   |
   v
Show Play button
```

The first video chunk contains metadata:

```text
[260 bytes filename]
[8 bytes total file size]
[chunk bytes]
```

All later chunks contain only:

```text
[chunk bytes]
```

## Thumbnail Generation

After a video is fully sent or received, PeerChat generates a thumbnail locally using WPF `MediaPlayer`. The thumbnail is not sent over the network. Each side creates its own thumbnail from the video file available on that machine.

```text
Video file
   |
   v
MediaPlayer opens file
   |
   v
Seek near first frame
   |
   v
Render frame to bitmap
   |
   v
Bind bitmap to chat bubble
```

## Technologies Used

- C#
- WPF
- .NET Framework 4.8
- MVVM pattern
- TCP sockets
- `TcpClient`
- `TcpListener`
- `NetworkStream`
- XAML resource dictionaries

## How to Run

1. Open `PeerChat.sln` in Visual Studio.
2. Build the solution.
3. Run the application on two machines on the same network, or run two instances on one machine for local testing.
4. On one instance, choose Host mode and click Connect.
5. On the other instance, choose Client mode, enter the host IP address and port, then click Connect.
6. Start chatting.

Default port:

```text
9000
```

## Build Notes

This is a .NET Framework WPF project, so it is best built with Visual Studio or Visual Studio MSBuild.

The project has been verified with Visual Studio MSBuild and builds successfully.

## Current Limitations

- PeerChat supports one direct peer connection at a time.
- There is no encryption layer.
- There is no central user discovery server.
- File transfer resume is not implemented.
- Video transfer assumes only one video transfer is active at a time.

## Purpose

PeerChat is useful as a practical learning project for:

- TCP socket programming
- Custom application protocols
- WPF MVVM development
- Binary file transfer
- Chunked video streaming over TCP
- UI progress updates during background network operations
