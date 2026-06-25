import { useCallback, useRef, useState } from 'react'
import { Upload, FileText, X, CheckCircle, AlertCircle, ExternalLink } from 'lucide-react'

interface Props {
  onClose: () => void
  onContentReady: (html: string, filename: string) => void
}

type ConversionOption = 'convert' | 'as-is'
type Stage = 'drop' | 'options' | 'processing' | 'done' | 'error'

const FORMAT_MAP: Record<string, string> = {
  'text/plain': 'txt',
  'text/markdown': 'md',
  'text/x-markdown': 'md',
  'text/html': 'html',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'docx',
  'application/pdf': 'pdf',
}

const ACCEPTED = Object.keys(FORMAT_MAP).join(',') + ',.md,.txt,.docx'

function formatBytes(b: number) {
  if (b < 1024) return `${b} B`
  if (b < 1048576) return `${(b / 1024).toFixed(1)} KB`
  return `${(b / 1048576).toFixed(1)} MB`
}

function mdToHtml(md: string): string {
  const lines = md.replace(/\r\n/g, '\n').split('\n')
  const out: string[] = []
  let inCode = false
  let inList = false

  const inline = (s: string) =>
    s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
     .replace(/\*(.+?)\*/g, '<em>$1</em>')
     .replace(/`(.+?)`/g, '<code>$1</code>')
     .replace(/\[(.+?)\]\((.+?)\)/g, '<a href="$2">$1</a>')

  for (const line of lines) {
    if (line.startsWith('```')) {
      if (inCode) { out.push('</code></pre>'); inCode = false }
      else { out.push('<pre><code>'); inCode = true }
    } else if (inCode) {
      out.push(line.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'))
    } else if (line.startsWith('### ')) { if (inList) { out.push('</ul>'); inList = false } out.push(`<h3>${inline(line.slice(4))}</h3>`) }
    else if (line.startsWith('## '))  { if (inList) { out.push('</ul>'); inList = false } out.push(`<h2>${inline(line.slice(3))}</h2>`) }
    else if (line.startsWith('# '))   { if (inList) { out.push('</ul>'); inList = false } out.push(`<h1>${inline(line.slice(2))}</h1>`) }
    else if (line.startsWith('- ') || line.startsWith('* ')) {
      if (!inList) { out.push('<ul>'); inList = true }
      out.push(`<li>${inline(line.slice(2))}</li>`)
    } else if (/^\d+\. /.test(line)) {
      if (inList) { out.push('</ul>'); inList = false }
      out.push(`<li>${inline(line.replace(/^\d+\. /, ''))}</li>`)
    } else if (/^---+$/.test(line.trim())) {
      if (inList) { out.push('</ul>'); inList = false }
      out.push('<hr/>')
    } else if (line.trim() === '') {
      if (inList) { out.push('</ul>'); inList = false }
    } else {
      if (inList) { out.push('</ul>'); inList = false }
      out.push(`<p>${inline(line)}</p>`)
    }
  }
  if (inList) out.push('</ul>')
  return out.join('\n')
}

function txtToHtml(txt: string): string {
  return txt.replace(/\r\n/g, '\n')
    .split(/\n\n+/)
    .filter(p => p.trim())
    .map(p => `<p>${p.trim().replace(/\n/g, '<br/>')}</p>`)
    .join('\n')
}

export default function BlogUploadModal({ onClose, onContentReady }: Props) {
  const [stage, setStage] = useState<Stage>('drop')
  const [file, setFile] = useState<File | null>(null)
  const [format, setFormat] = useState('')
  const [option, setOption] = useState<ConversionOption>('convert')
  const [dragOver, setDragOver] = useState(false)
  const [preview, setPreview] = useState<string | null>(null)
  const [error, setError] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  const detectFormat = (f: File): string => {
    const mime = FORMAT_MAP[f.type]
    if (mime) return mime
    const ext = f.name.split('.').pop()?.toLowerCase() ?? ''
    return ['txt','md','html','docx','pdf'].includes(ext) ? ext : ''
  }

  const processFile = useCallback((f: File) => {
    const fmt = detectFormat(f)
    if (!fmt) { setError('Unsupported file type. Accepted: .txt, .md, .html, .docx, .pdf'); return }
    setFile(f)
    setFormat(fmt)
    setStage('options')
  }, [])

  const onDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault(); setDragOver(false)
    const f = e.dataTransfer.files[0]
    if (f) processFile(f)
  }, [processFile])

  const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]
    if (f) processFile(f)
  }

  const handleConvert = async () => {
    if (!file) return
    setStage('processing')

    try {
      if (format === 'txt' || format === 'md' || format === 'html') {
        const text = await file.text()
        let html = ''
        if (format === 'md')        html = mdToHtml(text)
        else if (format === 'html') html = text
        else                        html = txtToHtml(text)
        setPreview(html)
        setStage('done')
      } else {
        // docx / pdf — provide a download link; user can copy content manually
        const url = URL.createObjectURL(file)
        setPreview(url)
        setStage('done')
      }
    } catch {
      setError('Failed to process file. Please try again.')
      setStage('error')
    }
  }

  const handleUseContent = () => {
    if (!file || !preview) return
    if (format === 'docx' || format === 'pdf') {
      const html = `<p><em>Imported from ${file.name}.</em></p><p><a href="${preview}" download="${file.name}">Download original</a></p>`
      onContentReady(html, file.name)
    } else {
      onContentReady(preview, file.name)
    }
  }

  return (
    <div className="tiptap-modal-overlay" onClick={onClose}>
      <div className="upload-modal" onClick={e => e.stopPropagation()}>
        <div className="upload-modal-header">
          <h3>Import Document</h3>
          <button className="tiptap-toolbar-btn icon-only" onClick={onClose}><X size={16} /></button>
        </div>

        {stage === 'drop' && (
          <div
            className={`upload-dropzone${dragOver ? ' drag-over' : ''}`}
            onDrop={onDrop}
            onDragOver={e => { e.preventDefault(); setDragOver(true) }}
            onDragLeave={() => setDragOver(false)}
            onClick={() => inputRef.current?.click()}
          >
            <Upload size={36} className="upload-dropzone-icon" />
            <p className="upload-dropzone-label">Drop a file here or click to browse</p>
            <p className="upload-dropzone-hint">Supported: .txt, .md, .html, .docx, .pdf</p>
            <input ref={inputRef} type="file" accept={ACCEPTED} onChange={onFileChange} style={{ display: 'none' }} />
          </div>
        )}

        {stage === 'options' && file && (
          <div className="upload-options">
            <div className="upload-file-info">
              <FileText size={20} />
              <div>
                <div className="upload-filename">{file.name}</div>
                <div className="upload-filemeta">{format.toUpperCase()} · {formatBytes(file.size)}</div>
              </div>
            </div>

            {(format === 'txt' || format === 'md' || format === 'html') ? (
              <div className="upload-option-list">
                <label className={`upload-option-card${option === 'convert' ? ' selected' : ''}`}>
                  <input type="radio" name="opt" value="convert" checked={option === 'convert'}
                    onChange={() => setOption('convert')} />
                  <div>
                    <div className="upload-option-title">Convert to article</div>
                    <div className="upload-option-desc">Content flows into the editor for review and editing before submission.</div>
                  </div>
                </label>
                <label className={`upload-option-card${option === 'as-is' ? ' selected' : ''}`}>
                  <input type="radio" name="opt" value="as-is" checked={option === 'as-is'}
                    onChange={() => setOption('as-is')} />
                  <div>
                    <div className="upload-option-title">Use as-is</div>
                    <div className="upload-option-desc">Paste the raw content without conversion (e.g. you already have clean HTML).</div>
                  </div>
                </label>
              </div>
            ) : (
              <p className="upload-binary-note">
                {format === 'docx' ? 'Word documents' : 'PDF files'} will be linked in the article. You can download and copy content manually into the editor.
              </p>
            )}

            <div className="upload-modal-actions">
              <button className="tiptap-action-btn secondary" onClick={() => { setStage('drop'); setFile(null) }}>Back</button>
              <button className="tiptap-action-btn primary" onClick={handleConvert}>Continue</button>
            </div>
          </div>
        )}

        {stage === 'processing' && (
          <div className="upload-processing">
            <div className="upload-spinner" />
            <p>Processing {file?.name}…</p>
          </div>
        )}

        {stage === 'done' && preview && (
          <div className="upload-done">
            {format === 'docx' || format === 'pdf' ? (
              <div className="upload-binary-result">
                <CheckCircle size={24} className="upload-success-icon" />
                <p>File ready. A download link will be inserted into the article.</p>
                <a href={preview} download={file?.name} className="upload-download-link" target="_blank" rel="noreferrer">
                  <ExternalLink size={14} /> Preview / Download {file?.name}
                </a>
              </div>
            ) : (
              <div className="upload-html-preview">
                <div className="upload-preview-label">Preview</div>
                <div className="blog-article-content upload-preview-content"
                  dangerouslySetInnerHTML={{ __html: preview }} />
              </div>
            )}
            <div className="upload-modal-actions">
              <button className="tiptap-action-btn secondary" onClick={() => { setStage('options') }}>Back</button>
              <button className="tiptap-action-btn primary" onClick={handleUseContent}>
                Open in Editor
              </button>
            </div>
          </div>
        )}

        {stage === 'error' && (
          <div className="upload-error-state">
            <AlertCircle size={28} className="upload-error-icon" />
            <p>{error}</p>
            <button className="tiptap-action-btn secondary" onClick={() => { setStage('drop'); setError('') }}>Try Again</button>
          </div>
        )}
      </div>
    </div>
  )
}
