import axios, { type AxiosInstance } from 'axios';
import type { InferenceResponse, HealthStatus } from '../types';

class ApiClient {
    private client: AxiosInstance;

    constructor(baseUrl: string = 'http://localhost:8000') {
        this.client = axios.create({
            baseURL: baseUrl,
            timeout: 300000, // 5 minute timeout for large batches
            headers: {
                'Content-Type': 'application/json',
            },
        });

        // Add request interceptor for debugging
        this.client.interceptors.request.use(request => {
            console.log('API Request:', request.method?.toUpperCase(), request.url, request.baseURL);
            return request;
        });

        this.client.interceptors.response.use(
            response => response,
            error => {
                console.error('API Error:', {
                    url: error.config?.url,
                    baseURL: error.config?.baseURL,
                    method: error.config?.method,
                    status: error.response?.status,
                    data: error.response?.data
                });
                return Promise.reject(error);
            }
        );
    }

    setBaseUrl(baseUrl: string) {
        this.client.defaults.baseURL = baseUrl;
    }

    async health(): Promise<HealthStatus> {
        try {
            const response = await this.client.get('/health');
            return { status: 'ok', ...response.data };
        } catch (error) {
            return { status: 'error', message: String(error) };
        }
    }

    async uploadImages(files: File[]): Promise<string[]> {
        const formData = new FormData();
        files.forEach((file) => {
            formData.append('files', file);
        });

        const response = await this.client.post('/upload', formData, {
            headers: {
                'Content-Type': 'multipart/form-data',
            },
        });

        return response.data.paths;
    }

    async infer(
        imagePaths: string[],
        options: {
            topK?: number;
            device?: 'auto' | 'cpu' | 'cuda' | 'rocm';
            skipMissing?: boolean;
        } = {}
    ): Promise<InferenceResponse> {
        const { topK = 5, device = 'auto', skipMissing = false } = options;

        const response = await this.client.post('/infer', {
            items: imagePaths.map((path) => ({
                path,
                md5: null,
            })),
            top_k: topK,
            device,
            skip_missing: skipMissing,
        });

        return response.data;
    }

    async inferWithUpload(
        files: File[],
        options: {
            topK?: number;
            device?: 'auto' | 'cpu' | 'cuda' | 'rocm';
        } = {}
    ): Promise<InferenceResponse> {
        // First upload the files
        const paths = await this.uploadImages(files);

        // Then run inference
        return this.infer(paths, options);
    }
}

// Singleton instance
export const apiClient = new ApiClient();

// Hook-friendly function to update base URL
export const setApiBaseUrl = (url: string) => {
    apiClient.setBaseUrl(url);
};

export default apiClient;
