function [ ] = ichol_learn( )
%ICHOL_LEARN test learning operator with incomplete Cholesky
%

seed=32;
oldRng=rng();
rng(seed, 'twister');

se=BundleSerializer();
bunName='sigmoid_bw_proposal_10000';
%bunName = 'sigmoid_fw_proposal_5000';
% Nicolas's data. Has almost 30000 pairs.
%bunName=sprintf('nicolas_sigmoid_bw');
%bunName=sprintf('nicolas_sigmoid_fw');
%bunName=sprintf('simplegauss_d1_bw_proposal_30000' );
%bunName=sprintf('simplegauss_d1_fw_proposal_30000' );
%bunName='lds_d3_tox_3000';
bundle=se.loadBundle(bunName);

%n=5000;
%n=25000;
%[trBundle, teBundle] = bundle.partitionTrainTest(3000, 2000);
[trBundle, teBundle] = bundle.partitionTrainTest(5000, 4000);
%[trBundle, teBundle] = bundle.partitionTrainTest(500, 300);

%---------- options -----------
learner=ICholMapperLearner();
inTensor = bundle.getInputTensorInstances();
% median factors 
medf = [1/3, 1, 3];
%kernel_candidates=KEGaussian.productCandidatesAvgCov(inTensor, medf, 2000);
% in computing KGGaussian, non-Gaussian distributions will be treated as a Gaussian.
kernel_candidates = KGGaussian.productCandidatesAvgCov(inTensor, medf, 3000);
display(sprintf('Totally %d kernel candidates for ichol.', length(kernel_candidates)));

od=learner.getOptionsDescription();
display(' Learner options: ');
od.show();

% set my options
learner.opt('seed', seed);
learner.opt('kernel_candidates', kernel_candidates );
learner.opt('num_ho', 3);
learner.opt('ho_train_size', 1000);
learner.opt('ho_test_size', 500);
learner.opt('chol_tol', 1e-15);
learner.opt('chol_maxrank', 500);
learner.opt('use_multicore', false);
learner.opt('reglist', [1e-4, 1e-2, 1]);

s=learnMap(learner, trBundle, teBundle, bunName);
n=length(trBundle)+length(teBundle);
iden=sprintf('ichol_learn_%s_%s_%d.mat', class(learner), bunName, n);
fpath=Expr.scriptSavedFile(iden);

timeStamp=clock();
save(fpath, 's', 'timeStamp', 'trBundle', 'teBundle');

rng(oldRng);


end

function s=learnMap(learner, trBundle, teBundle, bunName)
    % run the specified learner. 
    % Return a struct S containing produced variables.

    assert(isa(learner, 'DistMapperLearner'));
    assert(isa(trBundle, 'MsgBundle'));

    n=length(trBundle)+length(teBundle);

    % learn a DistMapper
    [dm, learnerLog]=learner.learnDistMapper(trBundle);

    % test the learned DistMapper dm
    % KL or Hellinger
    divTester=DivDistMapperTester(dm);
    divTester.opt('div_function', 'KL'); 
    % test on the test MsgBundle
    %keyboard
    [Divs, outDa]=divTester.testDistMapper(teBundle);
    assert(isa(outDa, 'DistArray'));

    % Check improper messages
    impTester=ImproperDistMapperTester(dm);
    impOut=impTester.testDistMapper(teBundle);

    % save everything
    commit=GitTool.getCurrentCommit();
    timeStamp=clock();

    % Return a struct 
    s=struct();
    s.learner_class=class(learner);
    % type Options
    s.learner_options=learner.options;
    s.dist_mapper=dm;
    s.learner_log=learnerLog;
    s.div_tester=divTester;
    s.divs=Divs;
    s.out_distarray=outDa;
    s.imp_tester=impTester;
    s.imp_out=impOut;
    s.commit=commit;
    s.timeStamp=timeStamp;

end
