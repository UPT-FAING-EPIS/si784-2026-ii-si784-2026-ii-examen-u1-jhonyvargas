import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, resolveUrl } from '../api/client';
import { joinAuction, leaveAuction, subscribe } from '../api/realtime';
import type { AuctionClosedEvent, AuctionDetail as Auction, Bid, BidPlacedEvent } from '../api/types';
import { Countdown, ErrorMessage, Spinner, StatusBadge } from '../components/Common';
import { useAuth } from '../context/AuthContext';
import { formatDate, formatMoney } from '../utils/format';
import { validateBid } from '../utils/validation';

export function AuctionDetail() {
  const { id = '' } = useParams();
  const { user, isAdmin } = useAuth();
  const [auction, setAuction] = useState<Auction | null>(null);
  const [bids, setBids] = useState<Bid[]>([]);
  const [selectedImage, setSelectedImage] = useState(0);
  const [amount, setAmount] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [live, setLive] = useState(false);

  const load = useCallback(async () => {
    const [a, b] = await Promise.all([api.auction(id), api.bids(id)]);
    setAuction(a);
    setBids(b);
    setAmount((current) => current || a.nextMinimumBid.toFixed(2));
  }, [id]);

  useEffect(() => {
    load().catch((e: Error) => setError(e.message));
  }, [load]);

  // Suscripción en tiempo real a la subasta
  useEffect(() => {
    joinAuction(id)
      .then(() => setLive(true))
      .catch(() => setLive(false));

    const offBid = subscribe<BidPlacedEvent>('BidPlaced', (e) => {
      if (e.auctionId !== id) return;
      setBids((prev) => [e.bid, ...prev.filter((b) => b.id !== e.bid.id)].sort((x, y) => y.amount - x.amount));
      setAuction((prev) =>
        prev ? { ...prev, currentPrice: e.currentPrice, nextMinimumBid: e.nextMinimumBid, bidCount: e.bidCount } : prev,
      );
      setAmount((current) => (Number(current) < e.nextMinimumBid ? e.nextMinimumBid.toFixed(2) : current));
    });
    const offClosed = subscribe<AuctionClosedEvent>('AuctionClosed', (e) => {
      if (e.auctionId !== id) return;
      setInfo(e.winnerName ? `Subasta adjudicada a ${e.winnerName} por ${formatMoney(e.finalPrice)}.` : 'Subasta finalizada sin ofertas.');
      load().catch(() => undefined);
    });
    const offOther = [
      subscribe<{ auctionId: string }>('AuctionCancelled', (e) => e.auctionId === id && load().catch(() => undefined)),
      subscribe<{ auctionId: string }>('AuctionStarted', (e) => e.auctionId === id && load().catch(() => undefined)),
    ];

    return () => {
      offBid();
      offClosed();
      offOther.forEach((off) => off());
      void leaveAuction(id).catch(() => undefined);
    };
  }, [id, load]);

  if (!auction) return error ? <ErrorMessage message={error} /> : <Spinner />;

  const isSeller = user?.id === auction.sellerId;
  const canBid = !!user && !isSeller && auction.status === 'active';
  const images = auction.imageUrls.map((u) => resolveUrl(u) ?? '');

  const submitBid = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setInfo(null);
    const validation = validateBid(amount, auction.nextMinimumBid);
    if (validation) {
      setError(validation);
      return;
    }
    setSubmitting(true);
    try {
      await api.placeBid(auction.id, Number(amount));
      setInfo('¡Oferta registrada! Usted es el mejor postor.');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  const cancel = async () => {
    setError(null);
    try {
      await api.cancelAuction(auction.id);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="detail">
      <Link to="/" className="muted">
        ← Volver al listado
      </Link>
      <div className="detail-grid">
        <section className="gallery card">
          {images.length > 0 ? (
            <>
              <img className="gallery-main" src={images[selectedImage]} alt={auction.title} />
              {images.length > 1 && (
                <div className="thumbs">
                  {images.map((src, i) => (
                    <button
                      type="button"
                      key={src}
                      className={i === selectedImage ? 'thumb active' : 'thumb'}
                      onClick={() => setSelectedImage(i)}
                      aria-label={`Imagen ${i + 1}`}
                    >
                      <img src={src} alt="" />
                    </button>
                  ))}
                </div>
              )}
            </>
          ) : (
            <div className="no-image large">Sin imágenes</div>
          )}
        </section>

        <section className="card detail-info">
          <div className="row-between">
            <span className="muted">{auction.category}</span>
            <StatusBadge status={auction.status} />
          </div>
          <h1>{auction.title}</h1>
          <p className="muted small">
            Publicado por <strong>{auction.sellerName}</strong> · {formatDate(auction.createdAt)}
          </p>

          <div className="price-box">
            <span className="muted">{auction.bidCount > 0 ? 'Oferta actual' : 'Precio inicial'}</span>
            <strong className="price big">{formatMoney(auction.currentPrice)}</strong>
            <span className="muted small">
              {auction.bidCount} pujas · incremento mínimo {formatMoney(auction.minIncrement)}
            </span>
            {live && <span className="live-dot">En vivo</span>}
          </div>

          <dl className="facts">
            <dt>Inicio</dt>
            <dd>{formatDate(auction.startAt)}</dd>
            <dt>Cierre</dt>
            <dd>{formatDate(auction.endAt)}</dd>
            {auction.status === 'active' && (
              <>
                <dt>Tiempo restante</dt>
                <dd>
                  <Countdown to={auction.endAt} />
                </dd>
              </>
            )}
            {auction.status === 'upcoming' && (
              <>
                <dt>Comienza en</dt>
                <dd>
                  <Countdown to={auction.startAt} />
                </dd>
              </>
            )}
            {auction.status === 'finished' && (
              <>
                <dt>Adjudicado a</dt>
                <dd>{auction.winnerName ?? 'Sin ofertas'}</dd>
              </>
            )}
          </dl>

          <ErrorMessage message={error} />
          {info && <div className="alert alert-success">{info}</div>}

          {canBid && (
            <form className="bid-form" onSubmit={(e) => void submitBid(e)} noValidate>
              <label htmlFor="amount">Su oferta (mínimo {formatMoney(auction.nextMinimumBid)})</label>
              <div className="bid-input">
                <input
                  id="amount"
                  type="number"
                  step="0.01"
                  min={auction.nextMinimumBid}
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  required
                />
                <button type="submit" className="btn btn-primary" disabled={submitting}>
                  {submitting ? 'Enviando…' : 'Ofertar'}
                </button>
              </div>
            </form>
          )}
          {!user && auction.status === 'active' && (
            <p className="alert">
              <Link to="/login">Inicie sesión</Link> para ofertar.
            </p>
          )}
          {isSeller && auction.status !== 'finished' && auction.status !== 'cancelled' && (
            <div className="seller-actions">
              <span className="muted small">Usted es el vendedor de este artículo.</span>
              {(auction.bidCount === 0 || isAdmin) && (
                <button type="button" className="btn btn-danger" onClick={() => void cancel()}>
                  Cancelar subasta
                </button>
              )}
            </div>
          )}
        </section>
      </div>

      <section className="card">
        <h2>Descripción</h2>
        <p className="description">{auction.description}</p>
      </section>

      <section className="card">
        <h2>Historial de pujas</h2>
        {bids.length === 0 ? (
          <p className="muted">Aún no hay ofertas. ¡Sea el primero!</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Postor</th>
                  <th>Monto</th>
                  <th>Fecha</th>
                </tr>
              </thead>
              <tbody>
                {bids.map((b, i) => (
                  <tr key={b.id} className={i === 0 ? 'leader' : undefined}>
                    <td>
                      {b.bidderName}
                      {i === 0 && <span className="tag">Mejor oferta</span>}
                    </td>
                    <td>{formatMoney(b.amount)}</td>
                    <td>{formatDate(b.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
