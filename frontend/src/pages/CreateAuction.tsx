import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import type { Category } from '../api/types';
import { ErrorMessage } from '../components/Common';
import { toLocalInputValue } from '../utils/format';
import { hasErrors, validateAuction, validateImages, type AuctionForm, type Errors } from '../utils/validation';

const initialForm = (): AuctionForm => ({
  title: '',
  description: '',
  categoryId: '',
  startingPrice: '',
  minIncrement: '1.00',
  startAt: '',
  endAt: toLocalInputValue(new Date(Date.now() + 24 * 3_600_000)),
});

export function CreateAuction() {
  const navigate = useNavigate();
  const [categories, setCategories] = useState<Category[]>([]);
  const [form, setForm] = useState<AuctionForm>(initialForm);
  const [errors, setErrors] = useState<Errors<AuctionForm>>({});
  const [files, setFiles] = useState<File[]>([]);
  const [previews, setPreviews] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.categories().then(setCategories).catch(() => setCategories([]));
  }, []);

  useEffect(() => {
    const urls = files.map((f) => URL.createObjectURL(f));
    setPreviews(urls);
    return () => urls.forEach((u) => URL.revokeObjectURL(u));
  }, [files]);

  const set = (field: keyof AuctionForm) => (e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const onFiles = (e: ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(e.target.files ?? []);
    const problem = validateImages(selected);
    setError(problem);
    setFiles(problem ? [] : selected);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    const validation = validateAuction(form);
    setErrors(validation);
    const imageProblem = validateImages(files);
    if (hasErrors(validation) || imageProblem) {
      setError(imageProblem ?? 'Revise los campos marcados.');
      return;
    }

    setSaving(true);
    try {
      const created = await api.createAuction({
        title: form.title.trim(),
        description: form.description.trim(),
        categoryId: Number(form.categoryId),
        startingPrice: Number(form.startingPrice),
        minIncrement: Number(form.minIncrement),
        startAt: form.startAt ? new Date(form.startAt).toISOString() : null,
        endAt: new Date(form.endAt).toISOString(),
      });
      for (const file of files) {
        await api.uploadImage(created.id, file);
      }
      navigate(`/auctions/${created.id}`);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        const mapped: Errors<AuctionForm> = {};
        Object.entries(err.fieldErrors).forEach(([key, messages]) => {
          const field = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof AuctionForm;
          mapped[field] = messages[0];
        });
        setErrors(mapped);
      }
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const field = (name: keyof AuctionForm) => ({
    'aria-invalid': errors[name] ? true : undefined,
    className: errors[name] ? 'invalid' : undefined,
  });

  return (
    <div className="narrow">
      <h1>Publicar artículo</h1>
      <form className="card form" onSubmit={(e) => void submit(e)} noValidate>
        <label>
          Título *
          <input value={form.title} onChange={set('title')} maxLength={120} {...field('title')} />
          {errors.title && <small className="field-error">{errors.title}</small>}
        </label>

        <label>
          Descripción *
          <textarea rows={5} value={form.description} onChange={set('description')} maxLength={4000} {...field('description')} />
          {errors.description && <small className="field-error">{errors.description}</small>}
        </label>

        <label>
          Categoría *
          <select value={form.categoryId} onChange={set('categoryId')} {...field('categoryId')}>
            <option value="">Seleccione…</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
          {errors.categoryId && <small className="field-error">{errors.categoryId}</small>}
        </label>

        <div className="form-row">
          <label>
            Precio inicial (USD) *
            <input type="number" min="0.01" step="0.01" value={form.startingPrice} onChange={set('startingPrice')} {...field('startingPrice')} />
            {errors.startingPrice && <small className="field-error">{errors.startingPrice}</small>}
          </label>
          <label>
            Incremento mínimo *
            <input type="number" min="0.01" step="0.01" value={form.minIncrement} onChange={set('minIncrement')} {...field('minIncrement')} />
            {errors.minIncrement && <small className="field-error">{errors.minIncrement}</small>}
          </label>
        </div>

        <div className="form-row">
          <label>
            Inicio (opcional)
            <input type="datetime-local" value={form.startAt} onChange={set('startAt')} {...field('startAt')} />
            <small className="muted">Vacío = inicia inmediatamente</small>
            {errors.startAt && <small className="field-error">{errors.startAt}</small>}
          </label>
          <label>
            Cierre *
            <input type="datetime-local" value={form.endAt} onChange={set('endAt')} {...field('endAt')} />
            {errors.endAt && <small className="field-error">{errors.endAt}</small>}
          </label>
        </div>

        <label>
          Imágenes (máx. 5, 2 MB c/u)
          <input type="file" accept="image/jpeg,image/png,image/gif,image/webp" multiple onChange={onFiles} />
        </label>
        {previews.length > 0 && (
          <div className="previews">
            {previews.map((src) => (
              <img key={src} src={src} alt="Vista previa" />
            ))}
          </div>
        )}

        <ErrorMessage message={error} />
        <button type="submit" className="btn btn-primary" disabled={saving}>
          {saving ? 'Publicando…' : 'Publicar subasta'}
        </button>
      </form>
    </div>
  );
}
