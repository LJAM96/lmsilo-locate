import { useQuery } from '@tanstack/react-query'
import { Download, Filter, RefreshCw, FileText, Clock, User } from 'lucide-react'
import { useState } from 'react'

interface AuditLog {
    id: string
    service: string
    action: string
    timestamp: string
    username: string | null
    ip_address: string | null
    job_id: string | null
    file_name: string | null
    file_hash: string | null
    file_size_bytes: number | null
    processing_time_ms: number | null
    model_used: string | null
    status: string | null
}

export default function AuditLogViewer() {
    const [filters, setFilters] = useState({
        username: '',
        fromDate: '',
        toDate: '',
    })
    const [showFilters, setShowFilters] = useState(false)

    const { data: logs, isLoading, refetch } = useQuery<AuditLog[]>({
        queryKey: ['audit-logs', filters],
        queryFn: async () => {
            const params = new URLSearchParams()
            params.append('service', 'locate')
            if (filters.username) params.append('username', filters.username)
            if (filters.fromDate) params.append('from_date', filters.fromDate)
            if (filters.toDate) params.append('to_date', filters.toDate)
            const res = await fetch(`/api/audit?${params}`)
            return res.json()
        },
        refetchInterval: 10000,
    })

    const handleExport = async (format: 'csv' | 'json') => {
        const params = new URLSearchParams()
        params.append('format', format)
        params.append('service', 'locate')
        const res = await fetch(`/api/audit/export?${params}`)
        const blob = await res.blob()
        const url = URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.download = `locate_audit.${format}`
        a.click()
    }

    const formatTime = (ms: number | null) => {
        if (!ms) return '-'
        return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(1)}s`
    }

    return (
        <div className="p-6">
            <div className="flex items-center justify-between mb-6">
                <h2 className="text-xl font-semibold">Audit Log</h2>
                <div className="flex gap-2">
                    <button onClick={() => setShowFilters(!showFilters)} className="px-3 py-2 rounded bg-gray-100 dark:bg-gray-800">
                        <Filter className="w-4 h-4" />
                    </button>
                    <button onClick={() => refetch()} className="px-3 py-2 rounded bg-gray-100 dark:bg-gray-800">
                        <RefreshCw className="w-4 h-4" />
                    </button>
                    <button onClick={() => handleExport('csv')} className="px-3 py-2 rounded bg-accent text-white flex gap-2">
                        <Download className="w-4 h-4" /> Export
                    </button>
                </div>
            </div>

            {showFilters && (
                <div className="mb-4 p-4 bg-gray-100 dark:bg-gray-800 rounded-lg grid grid-cols-3 gap-4">
                    <input
                        type="text"
                        placeholder="Username"
                        value={filters.username}
                        onChange={(e) => setFilters({ ...filters, username: e.target.value })}
                        className="px-3 py-2 rounded border"
                    />
                    <input
                        type="datetime-local"
                        value={filters.fromDate}
                        onChange={(e) => setFilters({ ...filters, fromDate: e.target.value })}
                        className="px-3 py-2 rounded border"
                    />
                    <input
                        type="datetime-local"
                        value={filters.toDate}
                        onChange={(e) => setFilters({ ...filters, toDate: e.target.value })}
                        className="px-3 py-2 rounded border"
                    />
                </div>
            )}

            <div className="bg-white dark:bg-gray-900 rounded-lg overflow-hidden border">
                <table className="w-full text-sm">
                    <thead className="bg-gray-50 dark:bg-gray-800">
                        <tr>
                            <th className="px-4 py-3 text-left">Time</th>
                            <th className="px-4 py-3 text-left">Action</th>
                            <th className="px-4 py-3 text-left">User</th>
                            <th className="px-4 py-3 text-left">File</th>
                            <th className="px-4 py-3 text-left">Duration</th>
                            <th className="px-4 py-3 text-left">Status</th>
                        </tr>
                    </thead>
                    <tbody>
                        {logs?.map((log) => (
                            <tr key={log.id} className="border-t">
                                <td className="px-4 py-3">{new Date(log.timestamp).toLocaleString()}</td>
                                <td className="px-4 py-3">{log.action}</td>
                                <td className="px-4 py-3">{log.username || log.ip_address || '-'}</td>
                                <td className="px-4 py-3">{log.file_name || '-'}</td>
                                <td className="px-4 py-3">{formatTime(log.processing_time_ms)}</td>
                                <td className="px-4 py-3">
                                    <span className={`px-2 py-1 rounded text-xs ${log.status === 'success' ? 'bg-green-100 text-green-700' :
                                            log.status === 'failed' ? 'bg-red-100 text-red-700' : 'bg-gray-100'
                                        }`}>
                                        {log.status || '-'}
                                    </span>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {(!logs || logs.length === 0) && !isLoading && (
                    <div className="text-center py-12 text-gray-500">No audit logs</div>
                )}
            </div>
        </div>
    )
}
