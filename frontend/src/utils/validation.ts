/** Validaciones del lado del cliente (espejo de las reglas del backend). */

export type Errors<T> = Partial<Record<keyof T, string>>;

const USERNAME_PATTERN = /^[a-zA-Z0-9_.-]{3,50}$/;
const TWO_DECIMALS = /^\d+(\.\d{1,2})?$/;

/** Validación simple de correo sin expresiones regulares costosas. */
export function isValidEmail(value: string): boolean {
  const email = value.trim();
  if (email.length < 5 || email.length > 150 || /\s/.test(email)) return false;
  const at = email.indexOf('@');
  if (at < 1 || at !== email.lastIndexOf('@')) return false;
  const domain = email.slice(at + 1);
  const dot = domain.lastIndexOf('.');
  return dot > 0 && dot < domain.length - 1;
}

export const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
export const MAX_IMAGES = 5;
export const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];

export interface RegisterForm {
  userName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export function validateRegister(form: RegisterForm): Errors<RegisterForm> {
  const errors: Errors<RegisterForm> = {};
  if (!USERNAME_PATTERN.test(form.userName.trim()))
    errors.userName = 'Entre 3 y 50 caracteres: letras, números, ".", "_" o "-".';
  if (!isValidEmail(form.email)) errors.email = 'Ingrese un correo válido.';
  if (form.password.length < 8 || form.password.length > 100)
    errors.password = 'La contraseña debe tener entre 8 y 100 caracteres.';
  if (form.password !== form.confirmPassword) errors.confirmPassword = 'Las contraseñas no coinciden.';
  return errors;
}

export interface LoginForm {
  email: string;
  password: string;
}

export function validateLogin(form: LoginForm): Errors<LoginForm> {
  const errors: Errors<LoginForm> = {};
  if (!isValidEmail(form.email)) errors.email = 'Ingrese un correo válido.';
  if (!form.password) errors.password = 'Ingrese su contraseña.';
  return errors;
}

export interface AuctionForm {
  title: string;
  description: string;
  categoryId: string;
  startingPrice: string;
  minIncrement: string;
  startAt: string;
  endAt: string;
}

export function validateAuction(form: AuctionForm, now: Date = new Date()): Errors<AuctionForm> {
  const errors: Errors<AuctionForm> = {};
  const title = form.title.trim();
  const description = form.description.trim();

  if (title.length < 3 || title.length > 120) errors.title = 'El título debe tener entre 3 y 120 caracteres.';
  if (description.length < 10 || description.length > 4000)
    errors.description = 'La descripción debe tener entre 10 y 4000 caracteres.';
  if (!form.categoryId) errors.categoryId = 'Seleccione una categoría.';

  if (!TWO_DECIMALS.test(form.startingPrice) || Number(form.startingPrice) <= 0)
    errors.startingPrice = 'Ingrese un precio mayor a 0 (máx. 2 decimales).';
  if (!TWO_DECIMALS.test(form.minIncrement) || Number(form.minIncrement) <= 0)
    errors.minIncrement = 'Ingrese un incremento mayor a 0 (máx. 2 decimales).';

  const start = form.startAt ? new Date(form.startAt) : now;
  if (form.startAt && start.getTime() < now.getTime() - 60_000)
    errors.startAt = 'La fecha de inicio no puede estar en el pasado.';

  if (!form.endAt) {
    errors.endAt = 'Indique la fecha y hora de cierre.';
  } else {
    const end = new Date(form.endAt);
    if (end.getTime() <= start.getTime() + 60_000) errors.endAt = 'El cierre debe ser al menos 1 minuto después del inicio.';
    else if (end.getTime() > start.getTime() + 90 * 24 * 3_600_000) errors.endAt = 'La subasta no puede durar más de 90 días.';
  }
  return errors;
}

export function validateImages(files: File[]): string | null {
  if (files.length > MAX_IMAGES) return `Puede adjuntar como máximo ${MAX_IMAGES} imágenes.`;
  for (const file of files) {
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) return `"${file.name}": formato no permitido (JPG, PNG, GIF o WEBP).`;
    if (file.size > MAX_IMAGE_BYTES) return `"${file.name}" supera los 2 MB.`;
  }
  return null;
}

export function validateBid(amount: string, minimum: number): string | null {
  if (!TWO_DECIMALS.test(amount)) return 'Ingrese un monto válido (máx. 2 decimales).';
  if (Number(amount) < minimum) return `La oferta mínima es ${minimum.toFixed(2)}.`;
  return null;
}

export const hasErrors = (errors: object): boolean => Object.keys(errors).length > 0;
