# FlexPhone iOS Installation Guide

## 📱 **Install FlexPhone on Your iPhone**

FlexPhone can be installed as a Progressive Web App (PWA) on your iPhone, giving you a native-like experience with full SIP calling capabilities.

### 🚀 **Installation Steps**

#### Method 1: Direct Installation (iOS 14.3+)
1. **Open Safari** on your iPhone
2. **Navigate to FlexPhone URL**: Enter the FlexPhone web address
3. **Add to Home Screen**:
   - Tap the Share button (📤) at the bottom of Safari
   - Scroll down and tap "Add to Home Screen"
   - Confirm by tapping "Add" in the top-right corner

#### Method 2: File Transfer Installation
1. **Copy the ios-pwa folder** to your web server or local hosting
2. **Access via iPhone**: Open Safari and navigate to your hosting URL
3. **Follow installation steps** from Method 1 above

### 📋 **Installation Files Included**

- **index.html** - Main application interface
- **app.js** - FlexPhone functionality with DTMF and auto-complete
- **styles.css** - Mobile-optimized styling
- **manifest.json** - PWA configuration
- **sw.js** - Service worker for offline functionality
- **Icons**: Various sizes for home screen and app store

### 🎯 **Features Available on iOS**

✅ **Full SIP Functionality**
- Make and receive calls
- DTMF tone generation
- Call management and history

✅ **Mobile-Optimized Interface**
- Touch-friendly dialer
- Auto-complete contacts
- SMS integration

✅ **Native-Like Experience**
- Standalone app appearance
- Home screen icon
- Offline functionality

✅ **iOS-Specific Features**
- VoiceOver accessibility support
- Safe area handling (iPhone X+)
- iOS keyboard integration

### ⚙️ **Configuration**

#### Initial Setup
1. **Launch FlexPhone** from your home screen
2. **Enter SIP Settings**:
   - Server: Your SIP provider
   - Username: Your SIP username
   - Password: Your SIP password
3. **Test Connection**: Make a test call to verify setup

#### Permissions Required
- **Microphone**: Required for voice calls
- **Notifications**: Optional for call alerts
- **Contacts**: Optional for auto-complete

### 🔧 **SIP Provider Setup**

#### Compatible Providers
- ✅ **CallCentric** - Full compatibility
- ✅ **VoIP.ms** - Complete feature set
- ✅ **Twilio** - Enterprise features
- ✅ **Flowroute** - Business-grade
- ✅ **Generic SIP** - Any standards-compliant provider

#### Configuration Examples

**CallCentric:**
```
Server: callcentric.com
Port: 5060
Username: [Your extension]
Password: [Your SIP password]
```

**VoIP.ms:**
```
Server: [yourdomain].voip.ms
Port: 5060
Username: [Your account]
Password: [Your SIP password]
```

### 📞 **Using FlexPhone**

#### Making Calls
1. **Open FlexPhone** from home screen
2. **Enter number** using the dialer or type in search box
3. **Tap call button** or select from auto-complete
4. **DTMF tones** play automatically as you dial

#### Managing Contacts
1. **Add contacts** by tapping the contact icon
2. **Auto-complete** suggests names as you type
3. **Recent calls** appear in call history

#### SMS Integration
1. **Type contact name** in search box
2. **Select SMS option** when available
3. **Send messages** through SIP provider

### 🛠 **Troubleshooting**

#### App Won't Install
- Ensure you're using Safari (not Chrome or other browsers)
- Check iOS version (14.3+ recommended)
- Clear Safari cache and try again

#### No Audio During Calls
- Check microphone permissions in Settings > FlexPhone
- Verify SIP provider supports WebRTC
- Test with different network (WiFi vs cellular)

#### Connection Issues
- Verify SIP credentials are correct
- Check firewall settings on your network
- Try different SIP server ports (5060, 5061)

### 📱 **System Requirements**

- **iOS**: 14.3 or later (16.0+ recommended)
- **Safari**: Latest version
- **Network**: WiFi or cellular data
- **Storage**: 10MB for app and cache

### 🔒 **Privacy & Security**

- **Local Storage**: Contacts stored locally on device
- **Encrypted Calls**: Supports SRTP when available
- **No Data Collection**: No analytics or tracking
- **Secure Login**: SIP credentials encrypted in transit

### 📞 **Support**

#### Getting Help
- **Built-in Help**: Available in app settings
- **Test Mode**: Use test extension for debugging
- **Logs**: Available in browser developer tools

#### Common Issues
- **Audio Problems**: Check iOS permissions
- **Connectivity**: Verify SIP provider settings
- **Performance**: Close other apps during calls

---

## 🎉 **Ready to Use!**

Once installed, FlexPhone provides a complete SIP calling solution on your iPhone with:

- 📞 **Professional calling** with DTMF support
- 📱 **Mobile-optimized** interface
- 🔄 **Offline functionality** when cached
- 📞 **Native-like experience** as a PWA

**Welcome to FlexPhone iOS!** 📱