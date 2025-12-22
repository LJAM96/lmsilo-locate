import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Trash2, RefreshCw, Clock, CheckCircle, XCircle, Loader2, MapPin } from 'lucide-react'
import toast from 'react-hot-toast'

interface GeolocationJob {
    id: string
    filename: string
    status: string
    error: string | null
    results: {
        predictions?: Array<{
            latitude: number
            longitude: number
            confidence: number
            location_name?: string
        }>
    } | null
    top_k: number
    created_at: string
    completed_at: string | null
}

export default function JobList() {
    const queryClient = useQueryClient()

    const { data: jobs, isLoading, refetch } = useQuery<GeolocationJob[]>({
        queryKey: ['locate-jobs'],
        queryFn: async () => {
            const res = await fetch('/api/jobs')
            return res.json()
        },
        refetchInterval: 3000,
    })

    const deleteMutation = useMutation({
        mutationFn: async (jobId: string) => {
            const res = await fetch(`/api/jobs/${jobId}`, { method: 'DELETE' })
            if (!res.ok) throw new Error('Failed to delete job')
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['locate-jobs'] })
            toast.success('Job deleted')
        },
        onError: () => {
            toast.error('Failed to delete job')
        },
    })

    const getStatusIcon = (status: string) => {
        switch (status) {
            case 'completed':
                return <CheckCircle className="w-5 h-5 text-green-500" />
            case 'failed':
                return <XCircle className="w-5 h-5 text-red-500" />
            case 'processing':
                return <Loader2 className="w-5 h-5 text-accent animate-spin" />
            default:
                return <Clock className="w-5 h-5 text-oatmeal-400" />
        }
    }

    return (
        <div className="p-6 max-w-6xl mx-auto">
            <div className="flex items-center justify-between mb-6">
                <h2 className="text-2xl font-display font-semibold text-oatmeal-900 dark:text-oatmeal-50">
                    Geolocation Jobs
                </h2>
                <button
                    onClick={() => refetch()}
                    className="px-3 py-2 rounded-lg bg-oatmeal-100 dark:bg-oatmeal-800 hover:bg-oatmeal-200 dark:hover:bg-oatmeal-700 flex items-center gap-2"
                >
                    <RefreshCw className="w-4 h-4" />
                    Refresh
                </button>
            </div>

            {isLoading ? (
                <div className="text-center py-12 text-oatmeal-500">
                    <Loader2 className="w-8 h-8 animate-spin mx-auto mb-2" />
                    Loading jobs...
                </div>
            ) : (
                <div className="space-y-4">
                    {jobs?.map((job) => (
                        <div
                            key={job.id}
                            className="bg-white dark:bg-oatmeal-900 rounded-xl p-4 border border-oatmeal-200 dark:border-oatmeal-700"
                        >
                            <div className="flex items-start justify-between">
                                <div className="flex items-start gap-3">
                                    {getStatusIcon(job.status)}
                                    <div>
                                        <div className="font-medium text-oatmeal-900 dark:text-oatmeal-50">
                                            {job.filename}
                                        </div>
                                        {job.results?.predictions && job.results.predictions.length > 0 && (
                                            <div className="mt-2 space-y-1">
                                                {job.results.predictions.slice(0, 3).map((pred, i) => (
                                                    <div key={i} className="flex items-center gap-2 text-sm text-oatmeal-600 dark:text-oatmeal-400">
                                                        <MapPin className="w-3 h-3" />
                                                        <span>
                                                            {pred.location_name || `${pred.latitude.toFixed(4)}, ${pred.longitude.toFixed(4)}`}
                                                        </span>
                                                        <span className="text-xs text-oatmeal-400">
                                                            ({(pred.confidence * 100).toFixed(1)}%)
                                                        </span>
                                                    </div>
                                                ))}
                                            </div>
                                        )}
                                        {job.error && (
                                            <div className="text-sm text-red-500 mt-2">
                                                Error: {job.error}
                                            </div>
                                        )}
                                    </div>
                                </div>
                                <div className="flex items-center gap-4">
                                    <div className="text-right text-sm text-oatmeal-500">
                                        {new Date(job.created_at).toLocaleString()}
                                    </div>
                                    <button
                                        onClick={() => deleteMutation.mutate(job.id)}
                                        className="p-2 rounded-lg hover:bg-red-100 dark:hover:bg-red-900/30 text-oatmeal-400 hover:text-red-500"
                                    >
                                        <Trash2 className="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                        </div>
                    ))}
                    {(!jobs || jobs.length === 0) && (
                        <div className="text-center py-12 text-oatmeal-500 bg-white dark:bg-oatmeal-900 rounded-xl border border-oatmeal-200 dark:border-oatmeal-700">
                            No geolocation jobs yet
                        </div>
                    )}
                </div>
            )}
        </div>
    )
}
