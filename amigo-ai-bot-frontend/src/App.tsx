import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Route, Routes, useLocation } from 'react-router-dom';
import { createWebSocket, fetchInitialData } from './services/UpLinkAPI';
import { FaSignInAlt, FaSignOutAlt } from 'react-icons/fa';
import './App.css';

// Define icon components explicitly to satisfy TypeScript
const SignInIcon = FaSignInAlt as React.FC;
const SignOutIcon = FaSignOutAlt as React.FC;

interface IndexQuote {
  name: string;
  ltp: number;
  change: number;
}

interface TradeSuggestion {
  action: string;
  target: number;
  stopLoss: number;
}

const Dashboard: React.FC<{ accessToken: string | null, setAccessToken: (token: string | null) => void }> = ({ accessToken, setAccessToken }) => {
  const [spotPrice, setSpotPrice] = useState<number | null>(null);
  const [indicators, setIndicators] = useState<any>(null);
  const [tradeSuggestion, setTradeSuggestion] = useState<TradeSuggestion | null>(null);
  const [indexQuotes, setIndexQuotes] = useState<IndexQuote[]>([]);

  useEffect(() => {
    if (!accessToken) return;

    const loadInitialData = async () => {
      try {
        const data = await fetchInitialData(accessToken);
        setSpotPrice(data.spotPrice);
        setIndicators(data.indicators);
        setIndexQuotes(data.indexQuotes);
      } catch (err) {
        console.error('Error fetching initial data:', err);
      }
    };
    loadInitialData();

    const connection = createWebSocket(
      accessToken,
      (data: any) => {
        setSpotPrice(data.spotPrice);
        setIndexQuotes(data.indexQuotes);
        if (data.indicators) setIndicators(data.indicators);
      },
      (suggestion: TradeSuggestion) => setTradeSuggestion(suggestion),
      (error: any) => console.error('WebSocket error:', error),
      () => console.log('WebSocket closed')
    );

    return () => {
      connection.stop();
    };
  }, [accessToken]);

  return (
    <div className="dashboard">
      <h2>Bank Nifty Futures: {spotPrice?.toFixed(2)}</h2>
      {indicators && (
        <div className="indicators">
          <p>Bollinger Bands: Upper: {indicators.bollingerBands?.upper.toFixed(2)}, Lower: {indicators.bollingerBands?.lower.toFixed(2)}</p>
          <p>MACD: {indicators.macd?.macdLine.toFixed(2)} / {indicators.macd?.signalLine.toFixed(2)}</p>
          <p>RSI: {indicators.rsi?.toFixed(2)}</p>
          <p>VWAP: {indicators.vwap?.toFixed(2)}</p>
        </div>
      )}
      {tradeSuggestion && (
        <div className="trade-suggestion">
          <h3>Trade Suggestion</h3>
          <p><strong>Action:</strong> {tradeSuggestion.action}</p>
          <p><strong>Target:</strong> {tradeSuggestion.target.toFixed(2)}</p>
          <p><strong>Stop Loss:</strong> {tradeSuggestion.stopLoss.toFixed(2)}</p>
        </div>
      )}
      <button onClick={() => setAccessToken(null)}>
        <SignOutIcon /> Logout
      </button>
    </div>
  );
};

const Callback: React.FC<{ setAccessToken: (token: string | null) => void }> = ({ setAccessToken }) => {
  const location = useLocation();

  useEffect(() => {
    const urlParams = new URLSearchParams(location.search);
    const code = urlParams.get('code');
    if (code) {
      const getAccessToken = async () => {
        try {
          const response = await fetch('http://localhost:5039/api/auth/get-access-token', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code }),
          });
          const data = await response.json();
          setAccessToken(data.accessToken);
          window.history.replaceState({}, document.title, '/');
        } catch (err) {
          console.error('Error fetching access token:', err);
        }
      };
      getAccessToken();
    }
  }, [location, setAccessToken]);

  return <div>Loading...</div>;
};

const App: React.FC = () => {
  const [accessToken, setAccessToken] = useState<string | null>(null);

  return (
    <Router>
      <div className="App">
        <header className="App-header">
          <h1>Amigo Trading Bot</h1>
        </header>
        <main>
          <Routes>
            <Route
              path="/"
              element={
                accessToken ? (
                  <Dashboard accessToken={accessToken} setAccessToken={setAccessToken} />
                ) : (
                  <button
                    onClick={() => {
                      fetch('http://localhost:5039/api/auth/login-url')
                        .then((res) => res.json())
                        .then((data) => (window.location.href = data.loginUrl))
                        .catch((err) => console.error('Error fetching login URL:', err));
                    }}
                  >
                    <SignInIcon /> Login with Upstox
                  </button>
                )
              }
            />
            <Route path="/callback" element={<Callback setAccessToken={setAccessToken} />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
};

export default App;