import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { AuctionSummary, UserBid } from '../api/types';
import { AuctionCard, EmptyState, ErrorMessage, Spinner, StatusBadge } from '../components/Common';
import { useAuth } from '../context/AuthContext';
import { formatDate, formatMoney } from '../utils/format';

type Tab = 'participating' | 'bids' | 'won' | 'published';

const TABS: { key: Tab; label: string }[] = [
  { key: 'participating', label: 'Participando' },
  { key: 'bids', label: 'Historial de pujas' },
  { key: 'won', label: 'Ganadas' },
  { key: 'published', label: 'Mis publicaciones' },
];

export function Dashboard() {
  const { user } = useAuth();
  const [tab, setTab] = useState<Tab>('participating');
  const [auctions, setAuctions] = useState<AuctionSummary[]>([]);
  const [bids, setBids] = useState<UserBid[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    const task =
      tab === 'bids' ? api.userBids().then(setBids) : api.userAuctions(tab).then(setAuctions);
    task.catch((e: Error) => setError(e.message)).finally(() => setLoading(false));
  }, [tab]);

  const renderContent = () => {
    if (loading) return <Spinner />;
    if (tab === 'bids') {
      if (bids.length === 0) return <EmptyState>Aún no ha realizado pujas.</EmptyState>;
      return (
        <div className="table-wrap card">
          <table>
            <thead>
              <tr>
                <th>Artículo</th>
                <th>Su oferta</th>
                <th>Precio actual</th>
                <th>Estado</th>
                <th>Resultado</th>
                <th>Fecha</th>
              </tr>
            </thead>
            <tbody>
              {bids.map((b) => (
                <tr key={b.id}>
                  <td>
                    <Link to={`/auctions/${b.auctionId}`}>{b.auctionTitle}</Link>
                  </td>
                  <td>{formatMoney(b.amount)}</td>
                  <td>{formatMoney(b.auctionCurrentPrice)}</td>
                  <td>
                    <StatusBadge status={b.auctionStatus} />
                  </td>
                  <td>{renderResult(b)}</td>
                  <td>{formatDate(b.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    }
    if (auctions.length === 0) {
      const messages: Record<Exclude<Tab, 'bids'>, string> = {
        participating: 'No participa en ninguna subasta todavía.',
        won: 'Todavía no ha ganado subastas.',
        published: 'No ha publicado artículos.',
      };
      return <EmptyState>{messages[tab]}</EmptyState>;
    }
    return (
      <div className="grid">
        {auctions.map((a) => (
          <AuctionCard key={a.id} auction={a} />
        ))}
      </div>
    );
  };

  return (
    <>
      <div className="row-between">
        <h1>Mi panel</h1>
        <Link to="/auctions/new" className="btn btn-primary">
          + Publicar artículo
        </Link>
      </div>
      <p className="muted">Hola, {user?.userName}. Aquí puede seguir sus subastas y pujas.</p>
      <div className="tabs">
        {TABS.map((t) => (
          <button key={t.key} type="button" className={tab === t.key ? 'tab active' : 'tab'} onClick={() => setTab(t.key)}>
            {t.label}
          </button>
        ))}
      </div>
      <ErrorMessage message={error} />
      {renderContent()}
    </>
  );
}

function renderResult(bid: UserBid) {
  if (bid.auctionStatus === 'finished') {
    return bid.isWinning ? <span className="tag tag-success">Ganada</span> : <span className="tag">No adjudicada</span>;
  }
  if (bid.auctionStatus === 'cancelled') return <span className="tag">Cancelada</span>;
  return bid.isWinning ? <span className="tag tag-success">Va ganando</span> : <span className="tag tag-warn">Superada</span>;
}
