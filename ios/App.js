/**
 * 📱 FlexPhone iOS App
 * React Native wrapper for the FlexPhone SIP client
 */

import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  Text,
  View,
  TouchableOpacity,
  TextInput,
  ScrollView,
  Alert,
  Dimensions,
  StatusBar,
  Platform
} from 'react-native';

const { width, height } = Dimensions.get('window');

const FlexPhoneApp = () => {
  const [activeTab, setActiveTab] = useState('dialer');
  const [dialNumber, setDialNumber] = useState('');
  const [connectionStatus, setConnectionStatus] = useState('Disconnected');
  const [sipConfig, setSipConfig] = useState({
    provider: 'FLEXPBX',
    server: 'flexpbx.local',
    port: '5070',
    username: '',
    password: '',
    displayName: ''
  });

  useEffect(() => {
    // Initialize FlexPhone services
    console.log('📱 FlexPhone iOS App initialized');
    setConnectionStatus('Ready');
  }, []);

  const keypadButtons = [
    ['1', '2', '3'],
    ['4', '5', '6'],
    ['7', '8', '9'],
    ['*', '0', '#']
  ];

  const handleKeypadPress = (digit) => {
    setDialNumber(prev => prev + digit);
  };

  const handleCall = () => {
    if (dialNumber) {
      Alert.alert('Calling', `Calling ${dialNumber}...`);
    } else {
      Alert.alert('Error', 'Please enter a number to call');
    }
  };

  const handleClear = () => {
    setDialNumber('');
  };

  const handleConnect = () => {
    if (sipConfig.username && sipConfig.password) {
      Alert.alert('Connecting', `Connecting to ${sipConfig.provider}...`);
      setConnectionStatus('Connecting...');
      setTimeout(() => {
        setConnectionStatus('Connected');
      }, 2000);
    } else {
      Alert.alert('Error', 'Please enter username and password');
    }
  };

  const renderDialer = () => (
    <View style={styles.dialer}>
      <View style={styles.display}>
        <Text style={styles.displayText}>
          {dialNumber || 'Enter number...'}
        </Text>
      </View>

      <View style={styles.keypad}>
        {keypadButtons.map((row, rowIndex) => (
          <View key={rowIndex} style={styles.keypadRow}>
            {row.map((digit) => (
              <TouchableOpacity
                key={digit}
                style={styles.keypadButton}
                onPress={() => handleKeypadPress(digit)}
                activeOpacity={0.7}
              >
                <Text style={styles.keypadButtonText}>{digit}</Text>
              </TouchableOpacity>
            ))}
          </View>
        ))}
      </View>

      <View style={styles.callButtons}>
        <TouchableOpacity style={styles.callButton} onPress={handleCall}>
          <Text style={styles.callButtonText}>📞 Call</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.clearButton} onPress={handleClear}>
          <Text style={styles.clearButtonText}>⌫ Clear</Text>
        </TouchableOpacity>
      </View>
    </View>
  );

  const renderContacts = () => (
    <View style={styles.tabContent}>
      <Text style={styles.emptyState}>👥 No contacts yet</Text>
      <Text style={styles.emptyStateDesc}>Add contacts to see them here</Text>
    </View>
  );

  const renderHistory = () => (
    <View style={styles.tabContent}>
      <Text style={styles.emptyState}>📋 No call history</Text>
      <Text style={styles.emptyStateDesc}>Your call history will appear here</Text>
    </View>
  );

  const renderMessages = () => (
    <View style={styles.tabContent}>
      <Text style={styles.emptyState}>💬 No messages</Text>
      <Text style={styles.emptyStateDesc}>SMS messages will appear here</Text>
    </View>
  );

  const renderSettings = () => (
    <ScrollView style={styles.settings}>
      <Text style={styles.settingsTitle}>SIP Configuration</Text>

      <View style={styles.formGroup}>
        <Text style={styles.label}>Provider</Text>
        <TextInput
          style={styles.input}
          value={sipConfig.provider}
          onChangeText={(text) => setSipConfig(prev => ({...prev, provider: text}))}
        />
      </View>

      <View style={styles.formGroup}>
        <Text style={styles.label}>Server</Text>
        <TextInput
          style={styles.input}
          value={sipConfig.server}
          onChangeText={(text) => setSipConfig(prev => ({...prev, server: text}))}
          placeholder="sip.example.com"
        />
      </View>

      <View style={styles.formGroup}>
        <Text style={styles.label}>Port</Text>
        <TextInput
          style={styles.input}
          value={sipConfig.port}
          onChangeText={(text) => setSipConfig(prev => ({...prev, port: text}))}
          placeholder="5060"
          keyboardType="numeric"
        />
      </View>

      <View style={styles.formGroup}>
        <Text style={styles.label}>Username</Text>
        <TextInput
          style={styles.input}
          value={sipConfig.username}
          onChangeText={(text) => setSipConfig(prev => ({...prev, username: text}))}
          placeholder="your-username"
          autoCapitalize="none"
        />
      </View>

      <View style={styles.formGroup}>
        <Text style={styles.label}>Password</Text>
        <TextInput
          style={styles.input}
          value={sipConfig.password}
          onChangeText={(text) => setSipConfig(prev => ({...prev, password: text}))}
          placeholder="your-password"
          secureTextEntry
          autoCapitalize="none"
        />
      </View>

      <View style={styles.formGroup}>
        <Text style={styles.label}>Display Name</Text>
        <TextInput
          style={styles.input}
          value={sipConfig.displayName}
          onChangeText={(text) => setSipConfig(prev => ({...prev, displayName: text}))}
          placeholder="Your Name"
        />
      </View>

      <TouchableOpacity style={styles.connectButton} onPress={handleConnect}>
        <Text style={styles.connectButtonText}>🔗 Connect</Text>
      </TouchableOpacity>
    </ScrollView>
  );

  const renderTabContent = () => {
    switch (activeTab) {
      case 'dialer':
        return renderDialer();
      case 'contacts':
        return renderContacts();
      case 'history':
        return renderHistory();
      case 'messages':
        return renderMessages();
      case 'settings':
        return renderSettings();
      default:
        return renderDialer();
    }
  };

  return (
    <View style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#1e1e1e" />

      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.logo}>📱 FlexPhone</Text>
        <View style={styles.connectionInfo}>
          <View style={[
            styles.statusIndicator,
            { backgroundColor: connectionStatus === 'Connected' ? '#44ff44' : '#ff4444' }
          ]} />
          <Text style={styles.connectionStatus}>{connectionStatus}</Text>
        </View>
      </View>

      {/* Navigation */}
      <View style={styles.navTabs}>
        {[
          { id: 'dialer', icon: '📞', label: 'Call' },
          { id: 'contacts', icon: '👥', label: 'Contacts' },
          { id: 'history', icon: '📋', label: 'History' },
          { id: 'messages', icon: '💬', label: 'Messages' },
          { id: 'settings', icon: '⚙️', label: 'Settings' }
        ].map((tab) => (
          <TouchableOpacity
            key={tab.id}
            style={[
              styles.navTab,
              activeTab === tab.id && styles.activeNavTab
            ]}
            onPress={() => setActiveTab(tab.id)}
          >
            <Text style={[
              styles.navTabText,
              activeTab === tab.id && styles.activeNavTabText
            ]}>
              {tab.icon} {tab.label}
            </Text>
          </TouchableOpacity>
        ))}
      </View>

      {/* Content */}
      <View style={styles.content}>
        {renderTabContent()}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#1e1e1e',
  },
  header: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    paddingTop: Platform.OS === 'ios' ? 44 : 20,
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.1)',
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  logo: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '600',
  },
  connectionInfo: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  statusIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 8,
  },
  connectionStatus: {
    color: 'rgba(255, 255, 255, 0.7)',
    fontSize: 12,
  },
  navTabs: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.1)',
  },
  navTab: {
    flex: 1,
    padding: 12,
    alignItems: 'center',
  },
  activeNavTab: {
    backgroundColor: 'rgba(0, 123, 255, 0.2)',
    borderBottomWidth: 2,
    borderBottomColor: '#007bff',
  },
  navTabText: {
    color: 'rgba(255, 255, 255, 0.7)',
    fontSize: 10,
    textAlign: 'center',
  },
  activeNavTabText: {
    color: '#007bff',
  },
  content: {
    flex: 1,
    padding: 16,
  },
  dialer: {
    alignItems: 'center',
  },
  display: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    borderRadius: 8,
    padding: 16,
    marginBottom: 20,
    minHeight: 60,
    width: '100%',
    justifyContent: 'center',
    alignItems: 'center',
  },
  displayText: {
    color: '#ffffff',
    fontSize: 18,
    fontFamily: Platform.OS === 'ios' ? 'Menlo' : 'monospace',
  },
  keypad: {
    marginBottom: 20,
  },
  keypadRow: {
    flexDirection: 'row',
    marginBottom: 12,
  },
  keypadButton: {
    width: 70,
    height: 70,
    borderRadius: 35,
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    justifyContent: 'center',
    alignItems: 'center',
    marginHorizontal: 12,
  },
  keypadButtonText: {
    color: '#ffffff',
    fontSize: 24,
    fontWeight: 'bold',
  },
  callButtons: {
    flexDirection: 'row',
    gap: 16,
  },
  callButton: {
    backgroundColor: '#28a745',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 24,
  },
  callButtonText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 14,
  },
  clearButton: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 24,
  },
  clearButtonText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 14,
  },
  tabContent: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  emptyState: {
    color: 'rgba(255, 255, 255, 0.7)',
    fontSize: 18,
    marginBottom: 8,
  },
  emptyStateDesc: {
    color: 'rgba(255, 255, 255, 0.5)',
    fontSize: 14,
  },
  settings: {
    flex: 1,
  },
  settingsTitle: {
    color: '#ffffff',
    fontSize: 20,
    fontWeight: '600',
    marginBottom: 20,
  },
  formGroup: {
    marginBottom: 16,
  },
  label: {
    color: 'rgba(255, 255, 255, 0.8)',
    fontSize: 12,
    marginBottom: 4,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  input: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.2)',
    borderRadius: 6,
    padding: 12,
    color: '#ffffff',
    fontSize: 16,
  },
  connectButton: {
    backgroundColor: '#28a745',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
    marginTop: 20,
  },
  connectButtonText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 16,
  },
});

export default FlexPhoneApp;