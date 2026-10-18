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
        <div className="page-node-details"><strong title={page.url}>{page.url}</strong><span>Depth {page.depth} {page.httpStatusCode ? `· HTTP ${page.httpStatusCode}` : ''}</span></div>
        <div className="ratio-value"><strong>{ratio}%</strong><span>DOMAIN LINKS</span></div>
      </div>
      {page.children.length > 0 && <ul className="page-tree-children">{page.children.map((child) => <PageTreeNode key={child.pageId} page={child} />)}</ul>}
    </li>
  )
}

export default function CrawlResults({ job, maxDepth = 2, maxPages = 50 }) {
  const statusName = typeof job?.status === 'number'
    ? ['Pending', 'Running', 'Completed', 'Failed', 'Canceled'][job.status] || 'Unknown'
    : job?.status
  const pages = job?.pages ?? []
  const pageTree = buildPageTree(pages)
  const pageLimit = job?.maxPages ?? maxPages
  const progressPercent = pageLimit > 0 ? Math.min(100, Math.round(((job?.pagesProcessed ?? 0) / pageLimit) * 100)) : 0

  return (
    <section className="panel status-panel">
      <div className="panel-heading status-heading"><div className="heading-icon status-icon">⌁</div><div><h2>{job ? 'Latest crawl' : 'Ready when you are'}</h2><p>{job ? 'Your crawl run summary' : 'Your crawl activity will appear here'}</p></div></div>
      {job ? (
        <div className="result-content">
          <div className="result-status"><span className={`status-symbol ${statusName?.toLowerCase()}`}>{statusName === 'Completed' ? '✓' : statusName === 'Failed' ? '!' : '◷'}</span><div><strong>{statusName}</strong><small>{job.completedAt ? `Finished ${new Date(job.completedAt).toLocaleTimeString()}` : 'Crawl submitted'}</small></div><span className={`status-pill ${statusName?.toLowerCase()}`}>{statusName}</span></div>
          <div className="result-url"><span>START URL</span><strong title={job.inputUrl}>{job.inputUrl}</strong></div>
          {['Pending', 'Running'].includes(statusName) && <div className="crawl-progress" role="status" aria-live="polite"><div className="progress-label"><span>{statusName === 'Pending' ? 'Waiting to start' : 'Crawling pages'}</span><strong>{job.pagesProcessed ?? 0} / {pageLimit}</strong></div><div className="progress-track"><span style={{ width: `${progressPercent}%` }} /></div></div>}
          <div className="metrics-row"><div className="metric"><strong>{job.pagesProcessed ?? 0}</strong><span>Pages scanned</span></div><div className="metric"><strong>{job.maxDepth ?? maxDepth}</strong><span>Depth limit</span></div><div className="metric"><strong>{pageLimit}</strong><span>Page limit</span></div></div>
          <section className="page-tree-section" aria-label="Discovered pages">
            <div className="tree-heading"><div><strong>Discovered pages</strong><span>Page hierarchy and domain link ratio</span></div><span className="tree-count">{pages.length}</span></div>
            {pageTree.length > 0 ? <ul className="page-tree">{pageTree.map((page) => <PageTreeNode key={page.pageId} page={page} />)}</ul> : <p className="tree-empty">No pages were discovered for this crawl.</p>}
          </section>
          {job.failureReason && <div className="error-message result-error">{job.failureReason}</div>}
          <div className="job-id">JOB ID <span>{job.jobId}</span></div>
        </div>
      ) : (
        <div className="empty-state"><div className="empty-art"><div className="empty-ring ring-a" /><div className="empty-ring ring-b" /><div className="empty-center">⌕</div><span className="empty-star star-a">✳</span><span className="empty-star star-b">✦</span></div><strong>No crawls yet</strong><p>Configure a URL on the left and launch your first crawl. The results will show up here.</p><div className="empty-tip"><span>TIP</span> Start with a small page limit while testing.</div></div>
      )}
    </section>
  )
}