import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { API_URL, tokenStore } from './client';

/**
 * Conexión WebSocket (SignalR) compartida por toda la aplicación.
 * Se recrea al iniciar o cerrar sesión para enviar el token correcto.
 */
let connection: HubConnection | null = null;
let starting: Promise<HubConnection> | null = null;
const joinedAuctions = new Set<string>();

function build(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/auctions`, { accessTokenFactory: () => tokenStore.get() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export async function getConnection(): Promise<HubConnection> {
  if (connection?.state === HubConnectionState.Connected) return connection;
  if (starting) return starting;

  connection ??= build();
  const current = connection;
  current.onreconnected(() => {
    joinedAuctions.forEach((id) => void current.invoke('JoinAuction', id));
  });

  starting = current
    .start()
    .then(() => current)
    .finally(() => {
      starting = null;
    });
  return starting;
}

export async function resetConnection(): Promise<void> {
  const old = connection;
  connection = null;
  if (old) await old.stop();
  await getConnection().catch(() => undefined);
}

export async function joinAuction(auctionId: string): Promise<void> {
  joinedAuctions.add(auctionId);
  const conn = await getConnection();
  await conn.invoke('JoinAuction', auctionId);
}

export async function leaveAuction(auctionId: string): Promise<void> {
  joinedAuctions.delete(auctionId);
  if (connection?.state === HubConnectionState.Connected) {
    await connection.invoke('LeaveAuction', auctionId);
  }
}

/** Suscribe un manejador a un evento del hub y devuelve la función para cancelar la suscripción. */
export function subscribe<T>(eventName: string, handler: (payload: T) => void): () => void {
  let active = true;
  let conn: HubConnection | null = null;
  getConnection()
    .then((c) => {
      if (!active) return;
      conn = c;
      c.on(eventName, handler);
    })
    .catch(() => undefined);
  return () => {
    active = false;
    conn?.off(eventName, handler);
  };
}
