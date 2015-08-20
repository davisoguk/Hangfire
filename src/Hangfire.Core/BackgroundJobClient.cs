﻿// This file is part of Hangfire.
// Copyright © 2013-2014 Sergey Odinokov.
// 
// Hangfire is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// Hangfire is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with Hangfire. If not, see <http://www.gnu.org/licenses/>.

using System;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.States;

namespace Hangfire
{
    /// <summary>
    /// Represents a Hangfire Client API. Contains methods related
    /// to the job creation feature.
    /// </summary>
    public class BackgroundJobClient : IBackgroundJobClient
    {
        private readonly JobStorage _storage;
        private readonly IJobCreationProcess _process;
        private readonly IStateMachineFactoryFactory _stateMachineFactoryFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundJobClient"/> class
        /// with the default connection and default global 
        /// <see cref="DefaultJobCreationProcess"/> instance.
        /// </summary>
        public BackgroundJobClient()
            : this(JobStorage.Current)
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundJobClient"/> class
        /// with a specified storage and the default global
        /// <see cref="DefaultJobCreationProcess"/> instance.
        /// </summary>
        public BackgroundJobClient(JobStorage storage)
            : this(storage, new StateMachineFactoryFactory())
        {
        }

        public BackgroundJobClient(JobStorage storage, IStateMachineFactoryFactory stateMachineFactoryFactory)
            : this(storage, stateMachineFactoryFactory, new DefaultJobCreationProcess())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundJobClient"/> class
        /// with a specified job storage and job creation process.
        /// </summary>
        /// 
        /// <exception cref="ArgumentNullException"><paramref name="storage"/> argument is null.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="process"/> argument is null.</exception>
        public BackgroundJobClient(
            JobStorage storage,
            IStateMachineFactoryFactory stateMachineFactoryFactory, 
            IJobCreationProcess process)
        {
            if (storage == null) throw new ArgumentNullException("storage");
            if (stateMachineFactoryFactory == null) throw new ArgumentNullException("stateMachineFactoryFactory");
            if (process == null) throw new ArgumentNullException("process");
            
            _storage = storage;
            _stateMachineFactoryFactory = stateMachineFactoryFactory;
            _process = process;
        }

        /// <inheritdoc />
        public string Create(Job job, IState state)
        {
            if (job == null) throw new ArgumentNullException("job");
            if (state == null) throw new ArgumentNullException("state");

            try
            {
                using (var connection = _storage.GetConnection())
                {
                    var context = new CreateContext(connection, job, state);
                    var factory = _stateMachineFactoryFactory.CreateFactory(_storage);
                    var stateMachine = factory.Create(connection);

                    return _process.Run(context, stateMachine);
                }
            }
            catch (Exception ex)
            {
                throw new CreateJobFailedException("Job creation process has failed. See inner exception for details", ex);
            }
        }

        /// <inheritdoc />
        public bool ChangeState(string jobId, IState state, string fromState)
        {
            if (jobId == null) throw new ArgumentNullException("jobId");
            if (state == null) throw new ArgumentNullException("state");

            using (var connection = _storage.GetConnection())
            {
                var factory = _stateMachineFactoryFactory.CreateFactory(_storage);
                var stateMachine = factory.Create(connection);
                IState appliedState = stateMachine.ChangeState(jobId, state, fromState != null ? new[] { fromState } : null);

                return appliedState != null && appliedState.Name.Equals(state.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
