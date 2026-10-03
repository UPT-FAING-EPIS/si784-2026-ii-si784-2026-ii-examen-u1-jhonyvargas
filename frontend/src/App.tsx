import { BrowserRouter, Link, Route, Routes } from 'react-router-dom';
import { RequireAuth } from './components/Common';
import { Layout } from './components/Layout';
import { AuthProvider } from './context/AuthContext';
import { NotificationProvider } from './context/NotificationContext';
import { Admin } from './pages/Admin';
import { AuctionDetail } from './pages/AuctionDetail';
import { AuctionList } from './pages/AuctionList';
import { Login, Register } from './pages/AuthPages';
import { CreateAuction } from './pages/CreateAuction';
import { Dashboard } from './pages/Dashboard';
import { Notifications } from './pages/Notifications';

function NotFound() {
  return (
    <div className="empty">
      <h1>Página no encontrada</h1>
      <Link to="/">Volver al inicio</Link>
    </div>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <NotificationProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<AuctionList />} />
              <Route path="auctions/:id" element={<AuctionDetail />} />
              <Route path="login" element={<Login />} />
              <Route path="register" element={<Register />} />
              <Route
                path="auctions/new"
                element={
                  <RequireAuth>
                    <CreateAuction />
                  </RequireAuth>
                }
              />
              <Route
                path="dashboard"
                element={
                  <RequireAuth>
                    <Dashboard />
                  </RequireAuth>
                }
              />
              <Route
                path="notifications"
                element={
                  <RequireAuth>
                    <Notifications />
                  </RequireAuth>
                }
              />
              <Route
                path="admin"
                element={
                  <RequireAuth admin>
                    <Admin />
                  </RequireAuth>
                }
              />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </NotificationProvider>
    </AuthProvider>
  );
}
