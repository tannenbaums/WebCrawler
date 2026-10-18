import { useEffect, useState } from 'react'
import './App.css'

function buildPageTree(pages) {
  const nodes = new Map(pages.map((page) => [page.pageId, { ...page, children: [] }]))
  const roots = []

  pages.forEach((page) => {
    const node = nodes.get(page.pageId)
    const parent = page.parentPageId == null ? null : nodes.get(page.parentPageId)
    if (parent) parent.children.push(node)
    else roots.push(node)
  })

  return roots
}

function PageTreeNode({ page }) {
  const ratio = (Number(page.domainLinkRatio ?? 0) * 100).toFixed(1)

  return (
    <li className="page-tree-node">
      <div className="page-tree-row">
        <span className="page-node-icon">↗</span>
        <div className="page-node-details">
          <strong title={page.url}>{page.url}</strong>
          <span>Depth {page.depth} {page.httpStatusCode ? `· HTTP ${page.httpStatusCode}` : ''}</span>
        </div>
        <div className="ratio-value"><strong>{ratio}%</strong><span>DOMAIN LINKS</span></div>
      </div>
      {page.children.length > 0 && (
        <ul className="page-tree-children">
          {page.children.map((child) => <PageTreeNode key={child.pageId} page={child} />)}
        </ul>
      )}
    </li>
  )
}

function App() {
  const [url, setUrl] = useState('https://example.com')
  const [maxDepth, setMaxDepth] = useState(2)
  const [maxPages, setMaxPages] = useState(50)
  const [apiStatus, setApiStatus] = useState('checking')
  const [submitting, setSubmitting] = useState(false)
  const [job, setJob] = useState(null)
  const [history, setHistory] = useState([])
  const [selectedJobId, setSelectedJobId] = useState(null)
  const [historyLoading, setHistoryLoading] = useState(true)
  const [error, setError] = useState('')

  async function loadJob(jobId) {
    const response = await fetch(`/api/jobs/${jobId}`)
    if (!response.ok) throw new Error('Could not load this crawl.')
    const result = await response.json()
    setJob({ ...result.job, pages: result.pages ?? [] })
    setSelectedJobId(jobId)
  }

  useEffect(() => {
    fetch('/health')
      .then((response) => {
        if (!response.ok) throw new Error('API unavailable')
        return response.json()
      })
      .then(() => setApiStatus('online'))
      .catch(() => setApiStatus('offline'))

    fetch('/api/jobs?take=50')
      .then((response) => {
        if (!response.ok) throw new Error('Could not load crawl history.')
        return response.json()
      })
      .then(async (jobs) => {
        setHistory(jobs)
        if (jobs.length > 0) {
          await loadJob(jobs[0].jobId)
        }
      })
      .catch(() => setHistory([]))
      .finally(() => setHistoryLoading(false))
  }, [])

  async function startCrawl(event) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    setJob(null)

    try {
      const response = await fetch('/api/jobs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url, maxDepth: Number(maxDepth), maxPages: Number(maxPages) }),
      })
      const result = await response.json()
      if (!response.ok) throw new Error(result.message || 'Unable to start crawl.')
      setJob({ ...(result.job ?? result), pages: result.pages ?? [] })
      if (result.history) setHistory(result.history)
      setSelectedJobId(result.job?.jobId ?? result.jobId)
    } catch (requestError) {
      setError(requestError.message || 'Could not reach the API. Make sure it is running on port 5088.')
    } finally {
      setSubmitting(false)
    }
  }

  const statusName = typeof job?.status === 'number'
    ? ['Pending', 'Running', 'Completed', 'Failed', 'Canceled'][job.status] || 'Unknown'
    : job?.status
  const pageTree = buildPageTree(job?.pages ?? [])

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="WebCrawler home">
          <span className="brand-mark"><span /><span /><span /></span>
          <span>web<span className="brand-light">crawler</span></span>
        </a>
        <div className="topbar-right">
          <span className={`connection ${apiStatus}`}><i /> API {apiStatus}</span>
          <span className="environment">LOCAL DEVELOPMENT</span>
          <div className="avatar">WC</div>
        </div>
      </header>

      <main className="main-content">
        <section className="welcome-row">
          <div>
            <p className="eyebrow">CRAWL WORKSPACE <span className="eyebrow-line" /></p>
            <h1>Discover the web<span className="title-dot">.</span></h1>
            <p className="subtitle">Start a crawl and explore how a site is connected, one page at a time.</p>
          </div>
          <div className="hero-graphic" aria-hidden="true">
            <div className="orbit orbit-one" /><div className="orbit orbit-two" />
            <div className="globe"><span className="globe-latitude" /><span className="globe-longitude" /><span className="globe-node node-one" /><span className="globe-node node-two" /><span className="globe-node node-three" /></div>
          </div>
        </section>

        <div className="workspace-grid">
          <section className="panel crawl-panel">
            <div className="panel-heading">
              <div className="heading-icon">↗</div>
              <div><h2>New crawl</h2><p>Configure a website crawl</p></div>
              <span className="step-tag">01 <span>/ 01</span></span>
            </div>

            <form onSubmit={startCrawl}>
              <label htmlFor="crawl-url">Starting URL</label>
              <div className="url-input-wrap"><span className="url-icon">◎</span><input id="crawl-url" type="url" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="https://yourwebsite.com" required /></div>
              <p className="field-hint">Enter the page you want the crawler to begin from.</p>

              <div className="options-row">
                <div className="option-field"><label htmlFor="max-depth">Crawl depth</label><div className="select-wrap"><select id="max-depth" value={maxDepth} onChange={(event) => setMaxDepth(event.target.value)}>{[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map((depth) => <option key={depth} value={depth}>{depth} {depth === 1 ? 'level' : 'levels'}</option>)}</select><span>⌄</span></div><p className="field-hint">How many links deep to follow</p></div>
                <div className="option-field"><label htmlFor="max-pages">Page limit</label><div className="select-wrap"><select id="max-pages" value={maxPages} onChange={(event) => setMaxPages(event.target.value)}>{[10, 25, 50, 100, 250, 500, 1000].map((pages) => <option key={pages} value={pages}>{pages} pages</option>)}</select><span>⌄</span></div><p className="field-hint">Maximum pages to collect</p></div>
              </div>

              {error && <div className="error-message" role="alert"><span>!</span>{error}</div>}
              <button className="submit-button" type="submit" disabled={submitting || apiStatus === 'offline'}>
                {submitting ? <><span className="spinner" /> Crawling site…</> : <>Start crawling <span className="button-arrow">↗</span></>}
              </button>
              <p className="submit-note"><span className="shield">◇</span> Your crawl runs locally and results stay in your database.</p>
            </form>
          </section>

          <section className="panel status-panel">
            <div className="panel-heading status-heading">
              <div className="heading-icon status-icon">⌁</div>
              <div><h2>{job ? 'Latest crawl' : 'Ready when you are'}</h2><p>{job ? 'Your crawl run summary' : 'Your crawl activity will appear here'}</p></div>
            </div>

            {job ? (
              <div className="result-content">
                <div className="result-status"><span className={`status-symbol ${statusName?.toLowerCase()}`}>{statusName === 'Completed' ? '✓' : statusName === 'Failed' ? '!' : '◷'}</span><div><strong>{statusName}</strong><small>{job.completedAt ? `Finished ${new Date(job.completedAt).toLocaleTimeString()}` : 'Crawl submitted'}</small></div><span className={`status-pill ${statusName?.toLowerCase()}`}>{statusName}</span></div>
                <div className="result-url"><span>START URL</span><strong title={job.inputUrl}>{job.inputUrl}</strong></div>
                <div className="metrics-row"><div className="metric"><strong>{job.pagesProcessed ?? 0}</strong><span>Pages scanned</span></div><div className="metric"><strong>{maxDepth}</strong><span>Depth limit</span></div><div className="metric"><strong>{maxPages}</strong><span>Page limit</span></div></div>
                <section className="page-tree-section" aria-label="Discovered pages">
                  <div className="tree-heading"><div><strong>Discovered pages</strong><span>Page hierarchy and domain link ratio</span></div><span className="tree-count">{job.pages?.length ?? 0}</span></div>
                  {pageTree.length > 0 ? <ul className="page-tree">{pageTree.map((page) => <PageTreeNode key={page.pageId} page={page} />)}</ul> : <p className="tree-empty">No pages were discovered for this crawl.</p>}
                </section>
                {job.failureReason && <div className="error-message result-error">{job.failureReason}</div>}
                <div className="job-id">JOB ID <span>{job.jobId}</span></div>
              </div>
            ) : (
              <div className="empty-state">
                <div className="empty-art"><div className="empty-ring ring-a" /><div className="empty-ring ring-b" /><div className="empty-center">⌕</div><span className="empty-star star-a">✳</span><span className="empty-star star-b">✦</span></div>
                <strong>No crawls yet</strong><p>Configure a URL on the left and launch your first crawl. The results will show up here.</p>
                <div className="empty-tip"><span>TIP</span> Start with a small page limit while testing.</div>
              </div>
            )}
          </section>
        </div>

        <section className="panel history-panel">
          <div className="history-heading">
            <div><p className="eyebrow">YOUR ACTIVITY</p><h2>Crawl history</h2><p>Recent runs saved in your local database</p></div>
            <span className="history-total">{history.length} {history.length === 1 ? 'crawl' : 'crawls'}</span>
          </div>
          {historyLoading ? <p className="history-empty">Loading crawl history…</p> : history.length === 0 ? <p className="history-empty">No saved crawls yet. Your completed runs will appear here.</p> : (
            <div className="history-table-wrap">
              <div className="history-table-header"><span>START URL</span><span>STATUS</span><span>PAGES</span><span>STARTED</span><span /></div>
              <div className="history-list">
                {history.map((item) => (
                  <button className={`history-row ${selectedJobId === item.jobId ? 'selected' : ''}`} key={item.jobId} type="button" onClick={() => loadJob(item.jobId).catch((loadError) => setError(loadError.message))}>
                    <span className="history-url" title={item.inputUrl}><strong>{item.inputUrl}</strong><small>{item.jobId}</small></span>
                    <span><i className={`history-status-dot ${item.status.toLowerCase()}`} />{item.status}</span>
                    <span className="history-pages">{item.pagesProcessed}</span>
                    <span className="history-date">{new Date(item.createdAt).toLocaleString()}</span>
                    <span className="history-open">↗</span>
                  </button>
                ))}
              </div>
            </div>
          )}
        </section>

        <footer className="page-footer"><span><span className="footer-dot" /> DIRECT EXECUTION MODE</span><span>WEBCRAWLER <b>·</b> LOCAL INSTANCE</span></footer>
      </main>
    </div>
  )
}

export default App
