import { useCrawlDashboard } from './hooks/useCrawlDashboard'
import CrawlForm from './components/CrawlForm'
import CrawlHistory from './components/CrawlHistory'
import CrawlResults from './components/CrawlResults'
import './App.css'

function App() {
  const dashboard = useCrawlDashboard()
  const { apiStatus } = dashboard

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
          <CrawlForm apiStatus={dashboard.apiStatus} error={dashboard.error} onSubmit={dashboard.startCrawl} submitting={dashboard.submitting || dashboard.isCrawling} />
          <CrawlResults job={dashboard.job} />
        </div>

        <CrawlHistory error={dashboard.error} history={dashboard.history} loading={dashboard.historyLoading} onSelect={dashboard.selectJob} selectedJobId={dashboard.selectedJobId} />

        <footer className="page-footer"><span><span className="footer-dot" /> DIRECT EXECUTION MODE</span><span>WEBCRAWLER <b>·</b> LOCAL INSTANCE</span></footer>
      </main>
    </div>
  )
}

export default App
