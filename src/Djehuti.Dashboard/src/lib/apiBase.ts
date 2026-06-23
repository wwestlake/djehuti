// In dev the Vite proxy handles /api/ with no prefix.
// In production set VITE_API_BASE=/djehuti so all calls route under /djehuti/api/.
export const apiBase: string = (import.meta.env.VITE_API_BASE as string) ?? ''
