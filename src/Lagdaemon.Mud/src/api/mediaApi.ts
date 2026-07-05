const API = '/djehuti/api'

export interface MediaRecord {
  id: string
  uploaderId: string
  module: string
  contextId: string | null
  s3Key: string
  url: string
  filename: string
  contentType: string
  sizeBytes: number | null
  createdAt: string
}

export async function uploadToS3(
  file: File,
  module: string,
  contextId?: string
): Promise<MediaRecord> {
  const urlRes = await fetch(`${API}/media/upload-url`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ filename: file.name, contentType: file.type, module }),
  })
  if (!urlRes.ok) throw new Error('Failed to get upload URL')
  const { presignedUrl, s3Key } = await urlRes.json()

  const s3Res = await fetch(presignedUrl, {
    method: 'PUT',
    headers: { 'Content-Type': file.type },
    body: file,
  })
  if (!s3Res.ok) throw new Error('S3 upload failed')

  const confirmRes = await fetch(`${API}/media/confirm`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      s3Key,
      filename: file.name,
      contentType: file.type,
      sizeBytes: file.size,
      module,
      contextId: contextId ?? null,
    }),
  })
  if (!confirmRes.ok) throw new Error('Failed to confirm upload')
  return confirmRes.json()
}
