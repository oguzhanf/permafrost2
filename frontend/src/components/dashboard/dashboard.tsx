'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { LoadingSpinner } from '@/components/ui/loading-spinner';
import { Users, Shield, Activity, AlertTriangle, TrendingUp, Clock } from 'lucide-react';
import { formatRelativeTime, getStatusColor } from '@/lib/utils';
import Link from 'next/link';

export function Dashboard() {
  const { data: usersData, isLoading: usersLoading } = useQuery({
    queryKey: ['users', { page: 1, limit: 5 }],
    queryFn: () => api.users.getAll({ page: 1, limit: 5 }),
  });

  const { data: groupsData, isLoading: groupsLoading } = useQuery({
    queryKey: ['groups', { page: 1, limit: 5 }],
    queryFn: () => api.groups.getAll({ page: 1, limit: 5 }),
  });

  const { data: eventsData, isLoading: eventsLoading } = useQuery({
    queryKey: ['events', { page: 1, limit: 10 }],
    queryFn: () => api.events.getAll({ page: 1, limit: 10 }),
  });

  const { data: healthData } = useQuery({
    queryKey: ['health'],
    queryFn: () => api.health.check(),
    refetchInterval: 30000, // Refresh every 30 seconds
  });

  const stats = [
    {
      name: 'Total Users',
      value: usersData?.total || 0,
      icon: Users,
      color: 'text-blue-600',
      bgColor: 'bg-blue-100',
      href: '/dashboard/users',
    },
    {
      name: 'Total Groups',
      value: groupsData?.total || 0,
      icon: Shield,
      color: 'text-green-600',
      bgColor: 'bg-green-100',
      href: '/dashboard/groups',
    },
    {
      name: 'Recent Events',
      value: eventsData?.total || 0,
      icon: Activity,
      color: 'text-purple-600',
      bgColor: 'bg-purple-100',
      href: '/dashboard/events',
    },
    {
      name: 'System Health',
      value: healthData?.status === 'healthy' ? 'Healthy' : 'Issues',
      icon: healthData?.status === 'healthy' ? TrendingUp : AlertTriangle,
      color: healthData?.status === 'healthy' ? 'text-green-600' : 'text-red-600',
      bgColor: healthData?.status === 'healthy' ? 'bg-green-100' : 'bg-red-100',
      href: '/dashboard/settings',
    },
  ];

  return (
    <div className="py-6">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 md:px-8">
        {/* Header */}
        <div className="md:flex md:items-center md:justify-between">
          <div className="flex-1 min-w-0">
            <h2 className="text-2xl font-bold leading-7 text-gray-900 sm:text-3xl sm:truncate">
              Dashboard
            </h2>
            <p className="mt-1 text-sm text-gray-500">
              Overview of your Active Directory environment
            </p>
          </div>
          <div className="mt-4 flex md:mt-0 md:ml-4">
            <span className="text-sm text-gray-500">
              Last updated: {healthData ? formatRelativeTime(healthData.timestamp) : 'Unknown'}
            </span>
          </div>
        </div>

        {/* Stats */}
        <div className="mt-8">
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {stats.map((stat) => (
              <Link
                key={stat.name}
                href={stat.href}
                className="relative bg-white pt-5 px-4 pb-12 sm:pt-6 sm:px-6 shadow rounded-lg overflow-hidden hover:shadow-md transition-shadow"
              >
                <div>
                  <div className={`absolute ${stat.bgColor} rounded-md p-3`}>
                    <stat.icon className={`h-6 w-6 ${stat.color}`} />
                  </div>
                  <p className="ml-16 text-sm font-medium text-gray-500 truncate">
                    {stat.name}
                  </p>
                </div>
                <div className="ml-16 flex items-baseline pb-6 sm:pb-7">
                  <p className="text-2xl font-semibold text-gray-900">
                    {typeof stat.value === 'number' ? stat.value.toLocaleString() : stat.value}
                  </p>
                </div>
              </Link>
            ))}
          </div>
        </div>

        {/* Content Grid */}
        <div className="mt-8 grid grid-cols-1 gap-8 lg:grid-cols-2">
          {/* Recent Users */}
          <div className="bg-white shadow rounded-lg">
            <div className="px-4 py-5 sm:p-6">
              <div className="flex items-center justify-between">
                <h3 className="text-lg leading-6 font-medium text-gray-900">
                  Recent Users
                </h3>
                <Link
                  href="/dashboard/users"
                  className="text-sm font-medium text-primary-600 hover:text-primary-500"
                >
                  View all
                </Link>
              </div>
              <div className="mt-6">
                {usersLoading ? (
                  <div className="flex justify-center py-4">
                    <LoadingSpinner />
                  </div>
                ) : usersData?.data && usersData.data.length > 0 ? (
                  <div className="space-y-3">
                    {usersData.data.slice(0, 5).map((user) => (
                      <div key={user.id} className="flex items-center justify-between">
                        <div className="flex items-center">
                          <div className="flex-shrink-0">
                            <div className="h-8 w-8 rounded-full bg-gray-200 flex items-center justify-center">
                              <span className="text-sm font-medium text-gray-600">
                                {user.displayName?.charAt(0) || user.samAccountName.charAt(0)}
                              </span>
                            </div>
                          </div>
                          <div className="ml-3">
                            <p className="text-sm font-medium text-gray-900">
                              {user.displayName || user.samAccountName}
                            </p>
                            <p className="text-sm text-gray-500">{user.email}</p>
                          </div>
                        </div>
                        <span className={`badge ${getStatusColor(user.status)}`}>
                          {user.status}
                        </span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500">No users found</p>
                )}
              </div>
            </div>
          </div>

          {/* Recent Groups */}
          <div className="bg-white shadow rounded-lg">
            <div className="px-4 py-5 sm:p-6">
              <div className="flex items-center justify-between">
                <h3 className="text-lg leading-6 font-medium text-gray-900">
                  Recent Groups
                </h3>
                <Link
                  href="/dashboard/groups"
                  className="text-sm font-medium text-primary-600 hover:text-primary-500"
                >
                  View all
                </Link>
              </div>
              <div className="mt-6">
                {groupsLoading ? (
                  <div className="flex justify-center py-4">
                    <LoadingSpinner />
                  </div>
                ) : groupsData?.data && groupsData.data.length > 0 ? (
                  <div className="space-y-3">
                    {groupsData.data.slice(0, 5).map((group) => (
                      <div key={group.id} className="flex items-center justify-between">
                        <div className="flex items-center">
                          <div className="flex-shrink-0">
                            <Shield className="h-8 w-8 text-gray-400" />
                          </div>
                          <div className="ml-3">
                            <p className="text-sm font-medium text-gray-900">
                              {group.displayName || group.samAccountName}
                            </p>
                            <p className="text-sm text-gray-500">{group.description}</p>
                          </div>
                        </div>
                        <span className={`badge badge-${group.groupType === 'security' ? 'primary' : 'success'}`}>
                          {group.groupType}
                        </span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500">No groups found</p>
                )}
              </div>
            </div>
          </div>

          {/* Recent Events */}
          <div className="bg-white shadow rounded-lg lg:col-span-2">
            <div className="px-4 py-5 sm:p-6">
              <div className="flex items-center justify-between">
                <h3 className="text-lg leading-6 font-medium text-gray-900">
                  Recent Events
                </h3>
                <Link
                  href="/dashboard/events"
                  className="text-sm font-medium text-primary-600 hover:text-primary-500"
                >
                  View all
                </Link>
              </div>
              <div className="mt-6">
                {eventsLoading ? (
                  <div className="flex justify-center py-4">
                    <LoadingSpinner />
                  </div>
                ) : eventsData?.data && eventsData.data.length > 0 ? (
                  <div className="space-y-3">
                    {eventsData.data.slice(0, 8).map((event) => (
                      <div key={event.id} className="flex items-center justify-between">
                        <div className="flex items-center">
                          <div className="flex-shrink-0">
                            <Clock className="h-5 w-5 text-gray-400" />
                          </div>
                          <div className="ml-3">
                            <p className="text-sm font-medium text-gray-900">
                              {event.eventType.replace(/_/g, ' ')}
                            </p>
                            <p className="text-sm text-gray-500">
                              {event.domainControllerId} • {formatRelativeTime(event.occurredAt)}
                            </p>
                          </div>
                        </div>
                        <span className="badge badge-default">
                          {event.eventType}
                        </span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500">No events found</p>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
