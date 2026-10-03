import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useNotifications } from '../context/NotificationContext';

export function Layout() {
  const { user, isAdmin, logout } = useAuth();
  const { unread, toasts, dismissToast } = useNotifications();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <div className="app">
      <header className="topbar">
        <div className="container topbar-inner">
          <Link to="/" className="brand">
            <span className="brand-mark" aria-hidden="true">⚖</span> SubastaYa
          </Link>
          <nav className="nav">
            <NavLink to="/" end>
              Subastas
            </NavLink>
            {user && <NavLink to="/auctions/new">Publicar</NavLink>}
            {user && <NavLink to="/dashboard">Mi panel</NavLink>}
            {isAdmin && <NavLink to="/admin">Administración</NavLink>}
          </nav>
          <div className="nav-user">
            {user ? (
              <>
                <Link to="/notifications" className="bell" aria-label={`Notificaciones (${unread} sin leer)`}>
                  🔔{unread > 0 && <span className="badge-count">{unread > 99 ? '99+' : unread}</span>}
                </Link>
                <span className="user-name">{user.userName}</span>
                <button type="button" className="btn btn-ghost" onClick={handleLogout}>
                  Salir
                </button>
              </>
            ) : (
              <>
                <Link to="/login" className="btn btn-ghost">
                  Ingresar
                </Link>
                <Link to="/register" className="btn btn-primary">
                  Crear cuenta
                </Link>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="container main">
        <Outlet />
      </main>

      <footer className="footer">
        <div className="container">SubastaYa · Plataforma de subastas en línea en tiempo real</div>
      </footer>

      <section className="toasts" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className="toast">
            <span>{t.message}</span>
            <button type="button" aria-label="Cerrar" onClick={() => dismissToast(t.id)}>
              ×
            </button>
          </div>
        ))}
      </section>
    </div>
  );
}
