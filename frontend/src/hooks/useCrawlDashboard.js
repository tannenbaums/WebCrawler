import { useCallback, useEffect, useState } from 'react'

async function readJson(response, fallbackMessage) {
  const result = await response.json()
  if (!response.ok) throw new Error(result.message || fallbackMessage)
  return result
}

function getStatusName(status) {
  return typeof status === 'number'
    ? ['Pending', 'Running', 'Completed', 'Failed', 'Canceled'][status] || 'Unknown'
    : status
}

export function useCrawlDashboard() {
  const [apiStatus, setApiStatus] = useState('checking')
  const [job, setJob] = useState(null)
  const [history, setHistory] = useState([])
  const [selectedJobId, setSelectedJobId] = useState(null)
  const [historyLoading, setHistoryLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const loadJob = useCallback(async (jobId, signal) => {
    const response = await fetch(`/api/jobs/${jobId}`, { signal })
    const result = await readJson(response, 'Could not load this crawl.')
    setJob((currentJob) => ({
      ...result.job,
      pages: result.pages ?? [],
      maxPages: currentJob?.jobId === jobId ? currentJob.maxPages : undefined,
    }))
    setSelectedJobId(jobId)
    return result.job
  }, [])

  const refreshHistory = useCallback(async (signal) => {
    const response = await fetch('/api/jobs?take=50', { signal })
    const jobs = await readJson(response, 'Could not load crawl history.')
    setHistory(jobs)
    return jobs
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    fetch('/health/ready', { signal: controller.signal })
      .then((response) => {
        if (!response.ok) throw new Error('API unavailable')
        setApiStatus('online')
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setApiStatus('offline')
      })

    refreshHistory(controller.signal)
      .then((jobs) => jobs.length > 0 && loadJob(jobs[0].jobId, controller.signal))
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
      .finally(() => {
        if (!controller.signal.aborted) setHistoryLoading(false)
      })

    return () => controller.abort()
  }, [loadJob, refreshHistory])

  useEffect(() => {
    if (!job || !['Pending', 'Running'].includes(getStatusName(job.status))) return undefined

    const controller = new AbortController()
    let timeoutId
    const poll = async () => {
      try {
        const latestJob = await loadJob(job.jobId, controller.signal)
        if (['Completed', 'Failed', 'Canceled'].includes(getStatusName(latestJob.status))) {
          await refreshHistory(controller.signal)
          return
        }
      } catch (requestError) {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      }

      if (!controller.signal.aborted) timeoutId = setTimeout(poll, 800)
    }

    timeoutId = setTimeout(poll, 300)
    return () => {
      controller.abort()
      clearTimeout(timeoutId)
    }
  }, [job?.jobId, job?.status, loadJob, refreshHistory])

  const startCrawl = useCallback(async (request) => {
    setSubmitting(true)
    setError('')

    try {
      const response = await fetch('/api/jobs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      })
      const result = await readJson(response, 'Unable to start crawl.')
      const nextJob = {
        ...(result.job ?? result),
        pages: result.pages ?? [],
        maxDepth: request.maxDepth,
        maxPages: request.maxPages,
        pagesProcessed: 0,
      }
      setJob(nextJob)
      setSelectedJobId(nextJob.jobId)
      if (result.history) setHistory(result.history)
      else await refreshHistory()
    } catch (requestError) {
      setError(requestError.message || 'Could not reach the API. Make sure it is running on port 5088.')
    } finally {
      setSubmitting(false)
    }
  }, [refreshHistory])

  const selectJob = useCallback(async (jobId) => {
    setError('')
    try {
      await loadJob(jobId)
    } catch (requestError) {
      setError(requestError.message)
    }
  }, [loadJob])

  const statusName = getStatusName(job?.status)
  const isCrawling = ['Pending', 'Running'].includes(statusName)

  return { apiStatus, error, history, historyLoading, isCrawling, job, selectedJobId, selectJob, startCrawl, submitting }
}