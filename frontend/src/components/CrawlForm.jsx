import { useState } from 'react'

export default function CrawlForm({ apiStatus, error, onSubmit, submitting }) {
  const [url, setUrl] = useState('https://example.com')
  const [maxDepth, setMaxDepth] = useState(2)
  const [maxPages, setMaxPages] = useState(50)

  function handleSubmit(event) {
    event.preventDefault()
    onSubmit({ url, maxDepth: Number(maxDepth), maxPages: Number(maxPages) })
  }

  return (
    <section className="panel crawl-panel">
      <div className="panel-heading">
        <div className="heading-icon">↗</div>
        <div><h2>New crawl</h2><p>Configure a website crawl</p></div>
        <span className="step-tag">01 <span>/ 01</span></span>
      </div>
      <form onSubmit={handleSubmit}>
        <label htmlFor="crawl-url">Starting URL</label>
        <div className="url-input-wrap"><span className="url-icon">◎</span><input id="crawl-url" type="url" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="https://yourwebsite.com" required /></div>
        <p className="field-hint">Enter the page you want the crawler to begin from.</p>
        <div className="options-row">
          <div className="option-field"><label htmlFor="max-depth">Crawl depth</label><div className="select-wrap"><select id="max-depth" value={maxDepth} onChange={(event) => setMaxDepth(event.target.value)}>{[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map((depth) => <option key={depth} value={depth}>{depth} {depth === 1 ? 'level' : 'levels'}</option>)}</select><span>⌄</span></div><p className="field-hint">How many links deep to follow</p></div>
          <div className="option-field"><label htmlFor="max-pages">Page limit</label><div className="select-wrap"><select id="max-pages" value={maxPages} onChange={(event) => setMaxPages(event.target.value)}>{[10, 25, 50, 100, 250, 500, 1000].map((pages) => <option key={pages} value={pages}>{pages} pages</option>)}</select><span>⌄</span></div><p className="field-hint">Maximum pages to collect</p></div>
        </div>
        {error && <div className="error-message" role="alert"><span>!</span>{error}</div>}
        <button className="submit-button" type="submit" disabled={submitting || apiStatus !== 'online'}>
          {submitting ? <><span className="spinner" /> Crawling site…</> : <>Start crawling <span className="button-arrow">↗</span></>}
        </button>
        <p className="submit-note"><span className="shield">◇</span> Your crawl runs locally and results stay in your database.</p>
      </form>
    </section>
  )
}