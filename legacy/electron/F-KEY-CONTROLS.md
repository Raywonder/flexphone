# FlexPhone F-Key Controls and Keyboard Shortcuts

## Complete F-Key Mapping

### Primary Functions
- **F1** - Pickup Phone / Answer Incoming Call
- **F2** - Hang Up Call / Hang Up Phone
- **F3** - Hold / Unhold Current Call
- **F4** - Initiate Call Transfer
- **F5** - Start/Stop Call Recording
- **F6** - Hold/Unhold Current Call
- **F7** - Queue Login/Logout (FlexPBX only)
- **F8-F10** - Reserved for Future Features

### Volume Controls
- **F11** - Output Volume Down (Speaker/Headphones)
- **F12** - Output Volume Up (Speaker/Headphones)
- **Shift+F11** - Input Volume Down (Microphone)
- **Shift+F12** - Input Volume Up (Microphone)

### System Controls
- **Cmd+,** (Command+Comma) - Open Settings (macOS Standard)
- **Escape x3** - Minimize to System Tray
  - 1st Escape: Clear dial display
  - 2nd Escape: Warning message
  - 3rd Escape: Minimize to tray

## Existing Keyboard Shortcuts (Preserved)

### Dialing
- **0-9, *, #** - DTMF digit entry
- **Enter** - Pickup phone (empty) / Make call (with number)
- **Backspace** - Remove last digit
- **Arrow Keys** - Navigate dropdowns with screen reader support

### Audio Features
- **Auto Dial Tone** - 350Hz + 440Hz on pickup
- **DTMF Tones** - Dual-frequency generation
- **Volume Responsive** - All audio respects F11/F12 volume settings

## Implementation Details

### Volume System
```javascript
this.outputVolume = 0.1; // Controlled by F11/F12
this.inputVolume = 0.5;  // Controlled by Shift+F11/F12
```

### State Management
- **Phone States**: On-hook, Off-hook, In-call, On-hold
- **Volume Range**: 0% to 100% (0.0 to 1.0)
- **Visual Feedback**: Toast notifications for all F-key actions
- **Audio Integration**: Volume changes apply to active dial tones and DTMF

### F-Key Benefits
1. **Accessibility**: Screen reader users can operate phone without mouse
2. **Speed**: Instant access to common functions
3. **Consistency**: Standard F-key layout across all tabs
4. **Feedback**: Audio and visual confirmation of all actions

## Usage Examples

### Making a Call
1. **F1** - Pickup (hear dial tone)
2. **Type number** - Dial tone stops automatically
3. **Enter** - Place call

### Receiving a Call
1. **F1** - Answer call
2. **F3** - Put on hold (optional)
3. **F2** - Hang up

### Volume Adjustment
- **F12, F12, F12** - Increase output volume 30%
- **Shift+F11** - Decrease microphone sensitivity
- **F11** - Quick volume down for quiet environments

### System Tray
- **Esc, Esc, Esc** (within 2 seconds) - Minimize to tray
- Click tray icon to restore

## Technical Notes

### Audio Context Integration
- All volume controls use the same AudioContext
- DTMF volume: `this.dtmfVolume * this.outputVolume`
- Dial tone volume: `this.outputVolume * 0.5`

### Error Handling
- Invalid operations show appropriate warnings
- F-keys work globally (any tab)
- Visual feedback for all actions

### Queue Management (F7)
- **Primary System**: FlexPBX (default, always available when connected)
- **Third-party Support**: Asterisk, FreeSWITCH, 3CX (disabled by default)
- **Smart Toggle**: Cycles through logged-out → logged-in → paused → logged-in
- **Auto System Detection**: Uses FlexPBX when connected, falls back to enabled third-party systems
- **Queue Selection**: Shows dialog if multiple queues available
- **Status Announcements**: Screen reader announces current queue status and system
- **Multi-system Integration**: Reports to FlexPBX or third-party APIs through accessibility layer
- **Settings Required**: Third-party systems must be enabled in settings to use F7

### Future Extensions
- F8-F10 available for features like:
  - Speed dial slots
  - Conference controls
  - ✅ Call queue management (F7 - implemented)
  - Custom SIP commands

This F-key system maintains all existing stable functionality while adding professional-grade keyboard control for power users and accessibility compliance.