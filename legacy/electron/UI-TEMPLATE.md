# FlexPhone UI Template - Current Stable Configuration

## Overview
This document serves as the stable template for FlexPhone's UI architecture. This configuration is working and stable, and should be used as reference when implementing changes.

## Current Architecture

### Main Application Structure
```
FlexPhone.app
├── Main Window (Single window with tabbed interface)
├── About Dialog (Modal popup)
└── Incoming Call Overlay (Modal overlay)
```

### Tab-Based Navigation
Currently uses a single-window tabbed interface with these tabs:
- **Call** (default/active) - Main dialer interface
- **Contacts** - Contact management
- **History** - Call history
- **Messages** - SMS messages
- **SIP Clients** - Connected phones
- **Recorder** - Call recording
- **Settings** - Configuration

### Current Keyboard Shortcuts
- **0-9, *, #** - DTMF dialing
- **Enter** - Pickup phone (empty) / Make call (with number)
- **Enter again** - Hang up phone when off-hook
- **Backspace** - Remove digit
- **Escape** - Clear display
- **Arrow keys** - Navigate dropdowns with proper screen reader feedback

### Audio Features (Working & Stable)
- **DTMF Tones** - Dual-frequency generation (working)
- **Dial Tone** - 350Hz + 440Hz continuous tone on pickup (working)
- **Audio Device Detection** - Microphone/speaker enumeration (working)
- **Volume Control** - Separate DTMF and dial tone volumes (working)

### Accessibility Features (Working & Stable)
- **Screen Reader Support** - Clean announcements without "selection changed"
- **Boundary Feedback** - "Already at top" / "Last option, already at bottom"
- **Keyboard Navigation** - Full arrow key support in dropdowns
- **Auto-focus** - Cursor lands in dial field on startup
- **Proper Labels** - ARIA labels and hint text
- **Visual Focus** - Clear focus indicators

### Provider Configuration (Working & Stable)
- **Auto-help System** - Shows config info for 30 seconds
- **Auto-focus Flow** - After 10 seconds, moves to server field
- **Supported Providers**:
  - FlexPBX (flexpbx.local:5070)
  - CallCentric (callcentric.com:5060)
  - VoIP.ms (chicago.voip.ms:5060)
  - Twilio (yourdomain.sip.twilio.com:5060)
  - Google Voice (obihai.com:5060)
  - Custom (manual entry)

### UI Components (Current Implementation)

#### Dial Pad Interface
```html
<input type="text" class="display" id="dialerDisplay"
       placeholder="Dial a number or type a name"
       aria-label="Dial Pad"
       aria-describedby="dialHint"
       autofocus>
```

#### Button Layout
```
[Call] [Hang Up] [Clear] [SMS]
[Echo Test] (Quick action)
```

#### Navigation Tabs
```
[Call] [Contacts] [History] [Messages] [SIP Clients] [Recorder] [Settings]
```

### CSS Styling (Stable)
- **Dark Theme** - Consistent across all elements
- **Clean Buttons** - No emoticons, text-only labels
- **Proper Input Fields** - White text on dark background with focus indicators
- **Responsive Layout** - Adapts to window sizing

### JavaScript Architecture (Stable)
```javascript
class FlexPhone {
    // Core properties
    currentNumber = '';
    isConnected = false;
    currentCall = null;
    phoneOffHook = false;

    // Audio system
    audioContext = new AudioContext();
    dialToneOscillator = null;
    dtmfEnabled = true;

    // Key methods (working)
    handleEnterKey() - Pickup/hangup/call logic
    startDialTone() - 350Hz + 440Hz generation
    stopDialTone() - Clean oscillator cleanup
    playDTMFTone(digit) - Dual-frequency DTMF
    setupSelectKeyboardNavigation() - Dropdown navigation
}
```

### State Management (Current)
- **Phone States**: On-hook, Off-hook, In-call
- **Connection States**: Disconnected, Connected, Calling
- **UI States**: Tab selection, Provider selection, Audio device selection

### File Structure (Stable)
```
FlexPhone/
├── public/
│   ├── index.html (Main UI definition)
│   ├── app.js (Core application logic)
│   └── assets/ (Audio files, images)
├── package.json (Electron configuration)
└── main.js (Electron main process)
```

## Key Working Features to Preserve

1. **Dial Tone System** - Perfect 350Hz + 440Hz generation
2. **DTMF Generation** - Accurate dual-frequency tones
3. **Screen Reader Integration** - Clean, non-verbose announcements
4. **Keyboard Navigation** - Arrow keys in dropdowns
5. **Provider Auto-configuration** - Help text and auto-focus
6. **Audio Device Detection** - Real-time enumeration
7. **Enter Key Logic** - Pickup/hangup/call behavior
8. **Auto-focus Flow** - Direct cursor to dial field

## Implementation Notes

### What Works Well
- Single AudioContext for all audio generation
- Event-driven architecture for UI updates
- Clean separation between UI and SIP logic
- Proper ARIA accessibility implementation
- Visual focus management
- State tracking for phone operations

### Architecture Principles
- **Single Window Base** - All tabs within one window (current)
- **Modal Overlays** - For dialogs and incoming calls
- **Event-Driven Updates** - UI responds to state changes
- **Accessible by Default** - All controls keyboard accessible
- **Clean Audio Management** - Proper oscillator lifecycle

## Change Guidelines

When implementing new features:
1. **Preserve** all working audio functionality
2. **Maintain** keyboard accessibility
3. **Keep** screen reader compatibility
4. **Ensure** clean state management
5. **Test** with existing SIP providers
6. **Validate** Enter key behavior remains intact

This template represents a stable, working configuration that successfully integrates with FlexPBX and other SIP providers while providing excellent accessibility support.