import { useRef, useState } from 'react'
import { uploadToS3 } from '../../api/mediaApi'
import { Upload, X } from 'lucide-react'

interface Props {
  module: string
  contextId?: string
  currentUrl?: string
  onUploaded: (url: string) => void
  accept?: string
  label?: string
  previewShape?: 'circle' | 'rect'
}

export default function ImageUpload({
  module,
  contextId,
  currentUrl,
  onUploaded,
  accept = 'image/jpeg,image/png,image/gif,image/webp',
  label = 'Upload image',
  previewShape = 'rect',
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [preview, setPreview] = useState<string | null>(currentUrl ?? null)
  const [dragging, setDragging] = useState(false)

  const handleFile = async (file: File) => {
    if (!file.type.startsWith('image/')) { setError('Only image files are supported'); return }
    if (file.size > 5 * 1024 * 1024) { setError('Image must be under 5 MB'); return }
    setError(null)
    setUploading(true)
    const localPreview = URL.createObjectURL(file)
    setPreview(localPreview)
    try {
      const record = await uploadToS3(file, module, contextId)
      onUploaded(record.url)
    } catch (e) {
      setError('Upload failed — check your connection and try again')
      setPreview(currentUrl ?? null)
    } finally {
      setUploading(false)
    }
  }

  const onInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) handleFile(file)
  }

  const onDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setDragging(false)
    const file = e.dataTransfer.files?.[0]
    if (file) handleFile(file)
  }

  return (
    <div className="image-upload-wrap">
      <div
        className={`image-upload-zone${dragging ? ' dragging' : ''}${previewShape === 'circle' ? ' circle' : ''}`}
        onClick={() => !uploading && inputRef.current?.click()}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        role="button"
        tabIndex={0}
        onKeyDown={e => e.key === 'Enter' && inputRef.current?.click()}
        aria-label={label}
      >
        {preview ? (
          <img
            src={preview}
            alt="preview"
            className={`image-upload-preview${previewShape === 'circle' ? ' circle' : ''}`}
          />
        ) : (
          <div className="image-upload-placeholder">
            <Upload size={20} />
            <span>{label}</span>
            <span className="image-upload-hint">or drag and drop · max 5 MB</span>
          </div>
        )}
        {uploading && <div className="image-upload-overlay"><span className="image-upload-spinner" /></div>}
      </div>

      {preview && !uploading && (
        <button
          type="button"
          className="image-upload-clear"
          onClick={e => { e.stopPropagation(); setPreview(null); onUploaded('') }}
          title="Remove image"
        >
          <X size={14} />
        </button>
      )}

      {error && <p className="image-upload-error">{error}</p>}

      <input
        ref={inputRef}
        type="file"
        accept={accept}
        className="image-upload-input"
        onChange={onInputChange}
      />
    </div>
  )
}
