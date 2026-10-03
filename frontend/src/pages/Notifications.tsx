import { Link } from 'react-router-dom';
import { EmptyState } from '../components/Common';
import { useNotifications } from '../context/NotificationContext';
import { formatDate } from '../utils/format';

const ICONS: Record<string, string> = {
  NewBid: '💰',
  Outbid: '⚠️',
  AuctionClosed: '⏱️',
  AuctionWon: '🏆',
  AuctionSold: '✅',
  AuctionCancelled: '🚫',
};

export function Notifications() {
  const { notifications, unread, markRead, markAllRead } = useNotifications();

  return (
    <div className="narrow">
      <div className="row-between">
        <h1>Notificaciones</h1>
        {unread > 0 && (
          <button type="button" className="btn btn-ghost" onClick={() => void markAllRead()}>
            Marcar todas como leídas
          </button>
        )}
      </div>
      {notifications.length === 0 ? (
        <EmptyState>No tiene notificaciones.</EmptyState>
      ) : (
        <ul className="notification-list">
          {notifications.map((n) => (
            <li key={n.id} className={n.isRead ? 'card notification' : 'card notification unread'}>
              <span className="notification-icon" aria-hidden="true">
                {ICONS[n.type] ?? '🔔'}
              </span>
              <div className="notification-body">
                <p>{n.message}</p>
                <span className="muted small">{formatDate(n.createdAt)}</span>
              </div>
              <div className="notification-actions">
                {n.auctionId && (
                  <Link to={`/auctions/${n.auctionId}`} onClick={() => !n.isRead && void markRead(n.id)}>
                    Ver subasta
                  </Link>
                )}
                {!n.isRead && (
                  <button type="button" className="btn btn-ghost small" onClick={() => void markRead(n.id)}>
                    Marcar leída
                  </button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
