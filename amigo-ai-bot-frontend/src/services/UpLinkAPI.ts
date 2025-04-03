import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export const createWebSocket = (
  accessToken: string,
  onMessage: (data: any) => void,
  onTradeSuggestion: (suggestion: any) => void,
  onError: (error: any) => void,
  onClose: () => void
): HubConnection => {
  const connection = new HubConnectionBuilder()
    .withUrl(`http://localhost:5039/ws/market-data-feed?access_token=${accessToken}`) // Updated port
    .configureLogging(LogLevel.Information)
    .build();

  connection.on('ReceiveMarketData', (data: any) => {
    console.log('Received market data:', data);
    onMessage(data);
  });

  connection.on('ReceiveTradeSuggestion', (suggestion: any) => {
    console.log('Received trade suggestion:', suggestion);
    onTradeSuggestion(suggestion);
  });

  connection.onclose((error) => {
    console.log('SignalR connection closed:', error);
    onClose();
  });

  connection.start().catch((err) => {
    console.error('SignalR connection error:', err);
    onError(err);
  });

  return connection;
};

export const fetchInitialData = async (accessToken: string): Promise<any> => {
  const response = await fetch('http://localhost:5039/api/market/initial-data', { // Updated URL
    headers: { accesstoken: accessToken },
  });
  if (!response.ok) throw new Error('Failed to fetch initial data');
  return response.json();
};