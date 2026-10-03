import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../components/Common';
import { useAuth } from '../context/AuthContext';
import { hasErrors, validateLogin, validateRegister, type Errors, type LoginForm, type RegisterForm } from '../utils/validation';

export function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';
  const [form, setForm] = useState<LoginForm>({ email: '', password: '' });
  const [errors, setErrors] = useState<Errors<LoginForm>>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const validation = validateLogin(form);
    setErrors(validation);
    if (hasErrors(validation)) return;
    setBusy(true);
    setError(null);
    try {
      await login(form.email, form.password);
      navigate(from, { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="narrow auth">
      <h1>Ingresar</h1>
      <form className="card form" onSubmit={(e) => void submit(e)} noValidate>
        <label>
          Correo
          <input type="email" autoComplete="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          {errors.email && <small className="field-error">{errors.email}</small>}
        </label>
        <label>
          Contraseña
          <input
            type="password"
            autoComplete="current-password"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
          {errors.password && <small className="field-error">{errors.password}</small>}
        </label>
        <ErrorMessage message={error} />
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Ingresando…' : 'Ingresar'}
        </button>
        <p className="muted small">
          ¿No tiene cuenta? <Link to="/register">Regístrese</Link>
        </p>
      </form>
    </div>
  );
}

export function Register() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState<RegisterForm>({ userName: '', email: '', password: '', confirmPassword: '' });
  const [errors, setErrors] = useState<Errors<RegisterForm>>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const validation = validateRegister(form);
    setErrors(validation);
    if (hasErrors(validation)) return;
    setBusy(true);
    setError(null);
    try {
      await register(form.userName, form.email, form.password);
      navigate('/dashboard');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const input = (name: keyof RegisterForm, label: string, type = 'text', autoComplete?: string) => (
    <label>
      {label}
      <input
        type={type}
        autoComplete={autoComplete}
        value={form[name]}
        onChange={(e) => setForm({ ...form, [name]: e.target.value })}
        className={errors[name] ? 'invalid' : undefined}
      />
      {errors[name] && <small className="field-error">{errors[name]}</small>}
    </label>
  );

  return (
    <div className="narrow auth">
      <h1>Crear cuenta</h1>
      <form className="card form" onSubmit={(e) => void submit(e)} noValidate>
        {input('userName', 'Nombre de usuario', 'text', 'username')}
        {input('email', 'Correo', 'email', 'email')}
        {input('password', 'Contraseña (mín. 8 caracteres)', 'password', 'new-password')}
        {input('confirmPassword', 'Confirmar contraseña', 'password', 'new-password')}
        <ErrorMessage message={error} />
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Creando…' : 'Crear cuenta'}
        </button>
        <p className="muted small">
          ¿Ya tiene cuenta? <Link to="/login">Ingrese</Link>
        </p>
      </form>
    </div>
  );
}
