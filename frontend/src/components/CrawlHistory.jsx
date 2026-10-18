export default function CrawlHistory({ error, history, loading, onSelect, selectedJobId }) {
  return (
    <section className="panel history-panel">
      <div className="history-heading">
        <div><p className="eyebrow">YOUR ACTIVITY</p><h2>Crawl history</h2><p>Recent runs saved in your local database</p></div>
        <span className="history-total">{history.length} {history.length === 1 ? 'crawl' : 'crawls'}</span>
      </div>
      {error && <div className="error-message" role="alert"><span>!</span>{error}</div>}
      {loading ? <p className="history-empty">Loading crawl history…</p> : history.length === 0 ? <p className="history-empty">No saved crawls yet. Your completed runs will appear here.</p> : (
        <div className="history-table-wrap">
          <div className="history-table-header"><span>START URL</span><span>STATUS</span><span>PAGES</span><span>STARTED</span><span /></div>
          <div className="history-list">
            {history.map((item) => (
              <button className={`history-row ${selectedJobId === item.jobId ? 'selected' : ''}`} key={item.jobId} type="button" aria-pressed={selectedJobId === item.jobId} onClick={() => onSelect(item.jobId)}>
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
  )
}