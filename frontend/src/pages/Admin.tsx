import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { AdminStats, AuctionStatus, AuctionSummary, User } from '../api/types';
import { ErrorMessage, Spinner, StatusBadge } from '../components/Common';
import { useAuth } from '../context/AuthContext';
import { formatDate, formatMoney } from '../utils/format';

type Tab = 'auctions' | 'users';

interface PendingAction {
  label: string;
  run: () => Promise<void>;
}

export function Admin() {
  const { user: me } = useAuth();
  const [tab, setTab] = useState<Tab>('auctions');
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [auctions, setAuctions] = useState<AuctionSummary[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [status, setStatus] = useState<AuctionStatus | 'all'>('all');
  const [search, setSearch] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const [s, a, u] = await Promise.all([
      api.adminStats(),
      api.adminAuctions({ status, pageSize: 100, sort: 'newest' }),
      api.adminUsers(search.trim() || undefined),
    ]);
    setStats(s);
    setAuctions(a.items);
    setUsers(u);
  }, [status, search]);

  useEffect(() => {
    load().catch((e: Error) => setError(e.message));
  }, [load]);

  const confirm = (label: string, action: () => Promise<unknown>) =>
    setPending({
      label,
      run: async () => {
        try {
          await action();
          await load();
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setPending(null);
        }
      },
    });

  if (!stats) return error ? <ErrorMessage message={error} /> : <Spinner />;

  return (
    <>
      <h1>Panel de administración</h1>
      <div className="stats">
        <Stat label="Usuarios" value={stats.totalUsers} />
        <Stat label="Subastas" value={stats.totalAuctions} />
        <Stat label="Activas" value={stats.activeAuctions} />
        <Stat label="Próximas" value={stats.upcomingAuctions} />
        <Stat label="Finalizadas" value={stats.finishedAuctions} />
        <Stat label="Pujas" value={stats.totalBids} />
        <Stat label="Monto adjudicado" value={formatMoney(stats.totalAwardedAmount)} />
      </div>

      {pending && (
        <div className="alert confirm" role="alertdialog">
          <span>¿Confirma: {pending.label}?</span>
          <div>
            <button type="button" className="btn btn-danger" onClick={() => void pending.run()}>
              Confirmar
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setPending(null)}>
              Cancelar
            </button>
          </div>
        </div>
      )}
      <ErrorMessage message={error} />

      <div className="tabs">
        <button type="button" className={tab === 'auctions' ? 'tab active' : 'tab'} onClick={() => setTab('auctions')}>
          Subastas
        </button>
        <button type="button" className={tab === 'users' ? 'tab active' : 'tab'} onClick={() => setTab('users')}>
          Usuarios
        </button>
      </div>

      {tab === 'auctions' ? (
        <section className="card">
          <div className="row-between">
            <h2>Gestión de subastas</h2>
            <select value={status} onChange={(e) => setStatus(e.target.value as AuctionStatus | 'all')} aria-label="Estado">
              <option value="all">Todas</option>
              <option value="active">Activas</option>
              <option value="upcoming">Próximas</option>
              <option value="finished">Finalizadas</option>
              <option value="cancelled">Canceladas</option>
            </select>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Artículo</th>
                  <th>Vendedor</th>
                  <th>Precio</th>
                  <th>Pujas</th>
                  <th>Cierre</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {auctions.map((a) => {
                  const open = a.status === 'active' || a.status === 'upcoming';
                  return (
                    <tr key={a.id}>
                      <td>
                        <Link to={`/auctions/${a.id}`}>{a.title}</Link>
                      </td>
                      <td>{a.sellerName}</td>
                      <td>{formatMoney(a.currentPrice)}</td>
                      <td>{a.bidCount}</td>
                      <td>{formatDate(a.endAt)}</td>
                      <td>
                        <StatusBadge status={a.status} />
                      </td>
                      <td className="actions">
                        {open && (
                          <>
                            <button
                              type="button"
                              className="btn btn-ghost small"
                              onClick={() => confirm(`cerrar y adjudicar "${a.title}"`, () => api.adminCloseAuction(a.id))}
                            >
                              Cerrar
                            </button>
                            <button
                              type="button"
                              className="btn btn-danger small"
                              onClick={() => confirm(`cancelar "${a.title}"`, () => api.adminCancelAuction(a.id))}
                            >
                              Cancelar
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      ) : (
        <section className="card">
          <div className="row-between">
            <h2>Gestión de usuarios</h2>
            <input
              type="search"
              placeholder="Buscar usuario…"
              value={search}
              maxLength={100}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Buscar usuario"
            />
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Correo</th>
                  <th>Rol</th>
                  <th>Estado</th>
                  <th>Registro</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td>{u.userName}</td>
                    <td>{u.email}</td>
                    <td>{u.role}</td>
                    <td>{u.isActive ? 'Activo' : 'Inactivo'}</td>
                    <td>{formatDate(u.createdAt)}</td>
                    <td className="actions">
                      {u.id !== me?.id && (
                        <>
                          <button
                            type="button"
                            className="btn btn-ghost small"
                            onClick={() =>
                              confirm(`cambiar el rol de ${u.userName}`, () =>
                                api.adminUpdateUser(u.id, { role: u.role === 'Admin' ? 'User' : 'Admin' }),
                              )
                            }
                          >
                            {u.role === 'Admin' ? 'Quitar admin' : 'Hacer admin'}
                          </button>
                          <button
                            type="button"
                            className={u.isActive ? 'btn btn-danger small' : 'btn btn-ghost small'}
                            onClick={() =>
                              confirm(`${u.isActive ? 'desactivar' : 'activar'} a ${u.userName}`, () =>
                                api.adminUpdateUser(u.id, { isActive: !u.isActive }),
                              )
                            }
                          >
                            {u.isActive ? 'Desactivar' : 'Activar'}
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </>
  );
}

function Stat({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div className="card stat">
      <span className="muted small">{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
