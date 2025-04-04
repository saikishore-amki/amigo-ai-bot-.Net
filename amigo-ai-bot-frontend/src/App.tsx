import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Route, Routes, useLocation } from 'react-router-dom';
import { createWebSocket, fetchInitialData } from './services/UpLinkAPI';
import { FaSignInAlt, FaSignOutAlt } from 'react-icons/fa';
import ErrorBoundary from './ErrorBoundary';
import './App.css';

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

const Callback: React.FC<{ setAccessToken: (token: string | null) => void; hasFetched: boolean; setHasFetched: (value: boolean) => void }> = ({ setAccessToken, hasFetched, setHasFetched }) => {
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    if (hasFetched) {
      console.log('Callback: Already fetched, skipping...');
      return;
    }

    const urlParams = new URLSearchParams(location.search);
    const code = urlParams.get('code');
    console.log('Callback triggered with code:', code);
    if (code && isMounted) {
      const getAccessToken = async () => {
        try {
          console.log('Sending POST to get-access-token with code:', code);
          const response = await fetch('http://localhost:5039/api/auth/get-access-token', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code }),
          });
          if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Failed to fetch access token: ${response.status} ${errorText}`);
          }
          const data = await response.json();
          console.log('Access token response:', data);
          if (isMounted) {
            setAccessToken(data.accessToken);
            setHasFetched(true);
            window.history.replaceState({}, document.title, '/');
          }
        } catch (err: unknown) {
          // Handle the unknown error type
          const errorMessage = err instanceof Error ? err.message : 'An unknown error occurred';
          console.error('Error fetching access token:', err);
          if (isMounted) {
            setError(errorMessage);
            setHasFetched(true);
          }
        }
      };
      getAccessToken();
    } else if (!code && isMounted) {
      console.error('No code found in callback URL');
      setError('No authorization code found in URL');
    }

    return () => {
      isMounted = false;
    };
  }, [location, setAccessToken, hasFetched, setHasFetched]);

  if (error) {
    return <div>Error: {error}. Please <a href="/">try again</a>.</div>;
  }

  return <div>Loading...</div>;
};

const App: React.FC = () => {
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [hasFetched, setHasFetched] = useState(false);

  return (
    <Router>
      <ErrorBoundary>
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
                        console.log('Fetching login URL');
                        fetch('http://localhost:5039/api/auth/login-url')
                          .then((res) => res.json())
                          .then((data) => {
                            console.log('Login URL received:', data.loginUrl);
                            window.location.href = data.loginUrl;
                          })
                          .catch((err) => console.error('Error fetching login URL:', err));
                      }}
                    >
                      <SignInIcon /> Login with Upstox
                    </button>
                  )
                }
              />
              <Route path="/callback" element={<Callback setAccessToken={setAccessToken} hasFetched={hasFetched} setHasFetched={setHasFetched} />} />
              <Route path="*" element={<div>Unexpected route accessed. Please return to <a href="/">home</a>.</div>} />
            </Routes>
          </main>
        </div>
      </ErrorBoundary>
    </Router>
  );
};

export default App;